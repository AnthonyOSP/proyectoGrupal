// Actualización en tiempo real con PieSocket (protocolo V4), sin librerías adicionales.
// Los eventos son solo una SEÑAL: al recibir uno, la página vuelve a pedirse a MVC (misma URL y filtros)
// y se reemplaza la zona marcada con data-realtime-region. Así lo que se muestra siempre sale de SQLite
// y un evento inesperado, como mucho, provoca un refresco. No hay polling.
(function () {
    const raiz = document.getElementById("realtime");
    if (!raiz || !("WebSocket" in window)) return;

    const url = raiz.dataset.url;          // wss://CLUSTER_ID.piesocket.com/v4
    const apiKey = raiz.dataset.apiKey;     // pública por diseño
    const canal = raiz.dataset.canal;       // private-...
    const urlAutorizar = raiz.dataset.autorizar;
    const token = raiz.dataset.token;       // antiforgery

    const estadoEl = raiz.querySelector(".realtime__estado");
    const textoEl = raiz.querySelector(".realtime__texto");
    const reintentarEl = raiz.querySelector(".realtime__reintentar");
    const aviso = document.getElementById("realtimeAviso");

    const ESPERAS = [1, 2, 4, 8, 16, 30, 30, 30]; // segundos entre reintentos (máximo 8 intentos)
    const TEXTOS = {
        conectando: "Tiempo real: conectando…",
        conectado: "Tiempo real: conectado",
        reconectando: "Tiempo real: reconectando…",
        desconectado: "Tiempo real: desconectado",
        sinPermiso: "Tiempo real no disponible para esta página"
    };

    let ws = null;
    let intentos = 0;
    let temporizador = null;
    let conectadoAlgunaVez = false;
    let cerrando = false;

    function ponerEstado(estado, texto) {
        estadoEl.dataset.estado = estado;
        textoEl.textContent = texto || TEXTOS[estado];
        reintentarEl.hidden = estado !== "desconectado";
    }

    // 1. JWT para este canal: lo firma el servidor después de comprobar permisos (contrato oficial: { auth }).
    async function pedirJwt() {
        const datos = new FormData();
        datos.append("channel_name", canal);
        datos.append("__RequestVerificationToken", token);
        const respuesta = await fetch(urlAutorizar, { method: "POST", body: datos, credentials: "same-origin", cache: "no-store" });
        const esJson = (respuesta.headers.get("content-type") || "").includes("application/json");
        if (!respuesta.ok || respuesta.redirected || !esJson) {
            // 401/403/404 o redirección al login: reintentar no lo arreglaría.
            const error = new Error("sin autorización");
            error.permanente = respuesta.status >= 400 && respuesta.status < 500 || respuesta.redirected;
            throw error;
        }
        const cuerpo = await respuesta.json();
        if (!cuerpo || typeof cuerpo.auth !== "string") throw new Error("respuesta inesperada");
        return cuerpo.auth;
    }

    // 2. Conexión V4: wss://CLUSTER_ID.piesocket.com/v4/CANAL?api_key=...&jwt=...  (be=1 pide "system::boot").
    async function conectar() {
        clearTimeout(temporizador);
        ponerEstado(conectadoAlgunaVez || intentos > 0 ? "reconectando" : "conectando");

        let jwt;
        try {
            jwt = await pedirJwt();
        } catch (e) {
            if (e.permanente) {
                ponerEstado("desconectado", TEXTOS.sinPermiso);
                reintentarEl.hidden = true;
                return;
            }
            programarReintento();
            return;
        }

        const direccion = url + "/" + encodeURIComponent(canal)
            + "?api_key=" + encodeURIComponent(apiKey)
            + "&notify_self=0&be=1&jwt=" + encodeURIComponent(jwt);

        let socket;
        try {
            socket = new WebSocket(direccion);
        } catch (e) {
            programarReintento();
            return;
        }
        ws = socket;

        // Si PieSocket no envía "system::boot" en unos segundos pero la conexión sigue abierta, se da por buena.
        let confirmacion = null;
        socket.addEventListener("open", () => {
            confirmacion = setTimeout(() => marcarConectado(), 3000);
        });

        // Cada manejador ignora los eventos de una conexión ya descartada (ws !== socket).
        socket.addEventListener("message", e => {
            if (ws !== socket) return;
            let mensaje;
            try { mensaje = JSON.parse(e.data); } catch { return; }
            if (!mensaje || typeof mensaje.event !== "string") return;

            if (mensaje.event === "system::boot") {
                clearTimeout(confirmacion);
                marcarConectado();
                return;
            }
            if (mensaje.event.startsWith("system::")) return; // p. ej. system::error: PieSocket cerrará la conexión
            if (mensaje["system::channel"] && mensaje["system::channel"] !== canal) return;

            manejarEvento(mensaje.event, mensaje.data || {});
        });

        socket.addEventListener("close", () => {
            clearTimeout(confirmacion);
            if (ws !== socket) return;
            ws = null;
            if (!cerrando) programarReintento();
        });
    }

    function marcarConectado() {
        if (!ws || ws.readyState !== WebSocket.OPEN || estadoEl.dataset.estado === "conectado") return;
        const eraReconexion = conectadoAlgunaVez;
        conectadoAlgunaVez = true;
        intentos = 0;
        ponerEstado("conectado");
        // Pudo haber cambios mientras no había conexión: se refresca una vez.
        if (eraReconexion) refrescar();
    }

    // 3. Reconexión con espera creciente (1, 2, 4, 8, 16, 30 s…) y un máximo de intentos.
    function programarReintento() {
        if (intentos >= ESPERAS.length) {
            ponerEstado("desconectado");
            return;
        }
        const espera = ESPERAS[intentos++] * (0.8 + Math.random() * 0.4) * 1000;
        ponerEstado("reconectando");
        temporizador = setTimeout(conectar, espera);
    }

    reintentarEl.addEventListener("click", () => { intentos = 0; conectar(); });

    // Sin red, el navegador puede tardar minutos en cerrar un WebSocket "medio abierto" y los avisos se pierden.
    // Por eso, al perder la red se descarta la conexión y al recuperarla se reconecta enseguida
    // (la reconexión refresca la página, así no se pierde ningún cambio). Solo eventos del navegador: sin polling.
    window.addEventListener("offline", () => {
        clearTimeout(temporizador);
        if (ws) {
            const anterior = ws;
            ws = null;
            try { anterior.close(); } catch { /* ya estaba cerrada */ }
        }
        if (!cerrando) ponerEstado("reconectando");
    });
    window.addEventListener("online", () => {
        if (!ws) { intentos = 0; conectar(); }
    });
    window.addEventListener("pagehide", () => { cerrando = true; clearTimeout(temporizador); if (ws) ws.close(); });

    // 4. Eventos de la aplicación.
    function manejarEvento(nombre, datos) {
        const id = Number(datos.incidenciaId) || 0;
        if (nombre === "incidencia.creada") {
            mostrarAviso("Nueva incidencia #" + id + ": «" + String(datos.titulo || "") + "»");
            refrescar(id);
        } else if (nombre === "incidencia.estado_actualizado") {
            const esSeguimiento = canal.startsWith("private-incidencia-");
            mostrarAviso(esSeguimiento
                ? "El estado cambió a «" + String(datos.estadoNuevo || "") + "»."
                : "La incidencia #" + id + " cambió a «" + String(datos.estadoNuevo || "") + "».");
            refrescar(id);
        }
    }

    // textContent: el texto que llega por el evento nunca se interpreta como HTML.
    let ocultarAviso = null;
    function mostrarAviso(texto) {
        if (!aviso) return;
        aviso.querySelector(".realtime-aviso__texto").textContent = texto;
        aviso.hidden = false;
        clearTimeout(ocultarAviso);
        ocultarAviso = setTimeout(() => { aviso.hidden = true; }, 8000);
    }
    if (aviso) aviso.querySelector(".realtime-aviso__cerrar").addEventListener("click", () => { aviso.hidden = true; });

    // 5. Refresco de la zona: la misma página MVC (GET normal) y se reemplaza solo data-realtime-region.
    //    Varios eventos seguidos se agrupan en un solo pedido.
    let esperaRefresco = null;
    const destacar = new Set();
    function refrescar(incidenciaId) {
        if (incidenciaId) destacar.add(String(incidenciaId));
        clearTimeout(esperaRefresco);
        esperaRefresco = setTimeout(async () => {
            const region = document.querySelector("[data-realtime-region]");
            if (!region) return;
            try {
                const respuesta = await fetch(location.href, { credentials: "same-origin", cache: "no-store" });
                if (!respuesta.ok || respuesta.redirected) return;
                const html = new DOMParser().parseFromString(await respuesta.text(), "text/html");
                const nueva = html.querySelector("[data-realtime-region]");
                if (!nueva) return;
                region.innerHTML = nueva.innerHTML;
                // Aviso genérico para otros componentes de la página (por ejemplo, el mapa del panel).
                document.dispatchEvent(new CustomEvent("realtime:actualizado"));
                destacar.forEach(id => region.querySelectorAll('[data-incidencia-id="' + id + '"]')
                    .forEach(el => el.classList.add("is-actualizado")));
                if (region.hasAttribute("data-realtime-destacar")) region.classList.add("is-actualizado");
                setTimeout(() => region.querySelectorAll(".is-actualizado").forEach(el => el.classList.remove("is-actualizado")), 4000);
                setTimeout(() => region.classList.remove("is-actualizado"), 4000);
            } catch {
                // Sin conexión con el servidor: la página sigue como estaba.
            } finally {
                destacar.clear();
            }
        }, 300);
    }

    conectar();
})();
