// Mapas de incidencias con Google Maps (Maps JavaScript API).
// Cada contenedor indica su modo con data-mapa:
//   "selector"     → formulario Reportar: el vecino marca o arrastra el punto (campos ocultos Latitud/Longitud).
//                    Además: autocompletado de la dirección escrita (Places API) y dirección del punto (Geocoding API).
//   "punto"        → Detalle y Seguimiento: un marcador fijo.
//   "incidencias"  → panel admin: marcadores de las incidencias filtradas (datos JSON en la página).
//   "zonas"        → panel admin: zonas críticas agrupadas con K-Means (datos JSON en la página).
// La clave llega en data-api-key del propio <script> (_GoogleMaps.cshtml). Sin clave, o si Google no
// carga, cada mapa muestra un aviso y el resto de la página sigue funcionando.
// Los textos se muestran con textContent (nunca como HTML).
(function () {
    const script = document.currentScript;
    const API_KEY = (script && script.dataset.apiKey) || "";
    const MAP_ID = (script && script.dataset.mapId) || "DEMO_MAP_ID";
    const contenedores = Array.from(document.querySelectorAll("[data-mapa]"));
    if (contenedores.length === 0) return;

    // --- Utilidades ---
    function esCoordenada(lat, lng) {
        return Number.isFinite(lat) && Number.isFinite(lng) && lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180;
    }

    // Google entrega posiciones como LatLng (con métodos) o como literal { lat, lng }.
    function aLiteral(p) {
        return typeof p.lat === "function" ? { lat: p.lat(), lng: p.lng() } : { lat: Number(p.lat), lng: Number(p.lng) };
    }

    function texto(etiqueta, contenido, clase) {
        const el = document.createElement(etiqueta);
        el.textContent = contenido;
        if (clase) el.className = clase;
        return el;
    }

    function temaOscuro() {
        return document.documentElement.getAttribute("data-theme") === "dark";
    }

    // --- Sin Google Maps: un aviso en lugar de cada mapa ---
    let sinMapas = false;
    function mostrarNoDisponible(mensaje) {
        if (sinMapas) return;
        sinMapas = true;
        contenedores.forEach(contenedor => {
            contenedor.hidden = true;
            const aviso = texto("p", mensaje, "mapa-sin-ubicacion mapa-no-disponible");
            aviso.setAttribute("role", "status");
            contenedor.after(aviso);
            // En Reportar se ocultan los botones del mapa (no tienen sentido sin él).
            const panel = contenedor.dataset.panel && document.getElementById(contenedor.dataset.panel);
            if (panel) panel.hidden = true;
        });
    }

    // Google llama a esta función si la clave no es válida o no tiene permiso en este dominio.
    window.gm_authFailure = () => mostrarNoDisponible("El mapa no está disponible en este momento (la clave de Google Maps no es válida para este sitio).");

    if (!API_KEY) {
        mostrarNoDisponible("El mapa no está disponible: falta configurar Google Maps.");
        return;
    }

    // --- Carga de la API de Google (una sola vez, en segundo plano) ---
    const urlApi = "https://maps.googleapis.com/maps/api/js?key=" + encodeURIComponent(API_KEY) +
        "&loading=async&callback=iniciarMapasIncidencias&language=es&region=PE&v=weekly";
    window.iniciarMapasIncidencias = iniciar;
    const cargador = document.createElement("script");
    cargador.src = urlApi;
    cargador.async = true;
    cargador.onerror = () => mostrarNoDisponible("El mapa no está disponible en este momento (no se pudo conectar con Google Maps).");
    document.head.appendChild(cargador);

    let G = null; // clases de Google que se usan (se cargan con importLibrary)

    async function iniciar() {
        try {
            const [maps, marker] = await Promise.all([
                google.maps.importLibrary("maps"),
                google.maps.importLibrary("marker")
            ]);
            G = {
                Map: maps.Map, InfoWindow: maps.InfoWindow, Circle: maps.Circle,
                LatLngBounds: google.maps.LatLngBounds, Marker: marker.AdvancedMarkerElement
            };
        } catch {
            mostrarNoDisponible("El mapa no está disponible en este momento (no se pudo conectar con Google Maps).");
            return;
        }
        if (sinMapas) return;

        for (const contenedor of contenedores) {
            const modo = contenedor.dataset.mapa;
            try {
                if (modo === "selector") await iniciarSelector(contenedor);
                else if (modo === "punto") iniciarPunto(contenedor);
                else if (modo === "incidencias") iniciarIncidencias(contenedor);
                else if (modo === "zonas") iniciarZonas(contenedor);
            } catch (error) {
                console.error("No se pudo iniciar el mapa", modo, error);
            }
        }
    }

    function crearMapa(contenedor, centro, zoom) {
        return new G.Map(contenedor, {
            center: centro,
            zoom: zoom,
            mapId: MAP_ID,
            colorScheme: temaOscuro() ? "DARK" : "LIGHT",
            // La rueda del mouse no "atrapa" el scroll de la página: para hacer zoom se usa Ctrl + rueda.
            gestureHandling: "cooperative",
            clickableIcons: false, // un clic sobre un negocio marca el punto, no abre su ficha
            mapTypeControl: false,
            streetViewControl: false,
            fullscreenControl: true
        });
    }

    // fitBounds sin acercarse más de maxZoom (por ejemplo, con un solo punto).
    function ajustar(mapa, limites, padding, maxZoom) {
        mapa.fitBounds(limites, padding);
        google.maps.event.addListenerOnce(mapa, "idle", () => {
            if (mapa.getZoom() > maxZoom) mapa.setZoom(maxZoom);
        });
    }

    // ancla: un marcador, o una posición { lat, lng } (por ejemplo, el centro de un círculo).
    function abrirPopup(info, mapa, contenido, ancla) {
        info.setContent(contenido);
        if (ancla instanceof G.Marker) info.open({ map: mapa, anchor: ancla });
        else { info.setPosition(ancla); info.open({ map: mapa }); }
    }

    // --- Formulario Reportar: elegir el punto y sincronizarlo con la dirección escrita ---
    async function iniciarSelector(contenedor) {
        const latInput = document.getElementById(contenedor.dataset.latitud);
        const lngInput = document.getElementById(contenedor.dataset.longitud);
        const direccionInput = document.getElementById(contenedor.dataset.direccion);
        const panel = document.getElementById(contenedor.dataset.panel);
        const latTexto = panel.querySelector("[data-mapa-latitud]");
        const lngTexto = panel.querySelector("[data-mapa-longitud]");
        const botonQuitar = panel.querySelector("[data-mapa-quitar]");
        const botonCentro = panel.querySelector("[data-mapa-centro]");
        const cajaDireccion = panel.querySelector("[data-mapa-direccion]");
        const textoDireccion = panel.querySelector("[data-mapa-direccion-texto]");
        const botonUsarDireccion = panel.querySelector("[data-mapa-direccion-usar]");

        // Si el formulario volvió con errores, se recupera el punto que ya se había marcado.
        const latPrevia = parseFloat(latInput.value), lngPrevia = parseFloat(lngInput.value);
        const hayPrevio = esCoordenada(latPrevia, lngPrevia);
        const mapa = crearMapa(contenedor,
            hayPrevio ? { lat: latPrevia, lng: lngPrevia } : { lat: Number(contenedor.dataset.centroLat), lng: Number(contenedor.dataset.centroLng) },
            hayPrevio ? 17 : Number(contenedor.dataset.zoom));
        let marcador = null;

        function avisarCambio(input) {
            input.dispatchEvent(new Event("change", { bubbles: true }));
        }

        // Dirección y punto: cada uno puede llenar al otro, sin pisar lo que el vecino escribió a mano.
        // "direccionAuto" es el último texto que puso el mapa: si el campo sigue igual, se puede reemplazar.
        let direccionAuto = null;
        let direccionPendiente = "";
        let consultaDireccion = 0;
        let geocoder = null;

        function ponerDireccion(valor) {
            const limite = Number(direccionInput.getAttribute("maxlength")) || 250;
            direccionInput.value = valor.slice(0, limite);
            direccionAuto = direccionInput.value;
            avisarCambio(direccionInput);
            // Si el campo tenía el mensaje "Indica dónde ocurre el problema", se vuelve a validar.
            if (window.jQuery && jQuery.fn.valid && direccionInput.classList.contains("input-validation-error")) jQuery(direccionInput).valid();
        }

        function mostrarDireccion(mensaje, conBoton) {
            cajaDireccion.hidden = !mensaje;
            textoDireccion.textContent = mensaje || "";
            botonUsarDireccion.hidden = !conBoton;
        }

        function limpiarDireccion(d) {
            return d.replace(/,\s*Per[uú]\s*$/i, "").trim();
        }

        async function buscarDireccionDelPunto(punto) {
            const id = ++consultaDireccion;
            mostrarDireccion("Buscando la dirección del punto…", false);
            try {
                if (!geocoder) {
                    const { Geocoder } = await google.maps.importLibrary("geocoding");
                    geocoder = new Geocoder();
                }
                const { results } = await geocoder.geocode({ location: punto, language: "es", region: "PE" });
                if (id !== consultaDireccion) return; // llegó una respuesta vieja: se ignora
                // Se evita el "plus code" (ej. "Q2X8+5F Lima") si hay una dirección de verdad.
                const resultado = (results || []).find(r => !(r.types || []).includes("plus_code")) || null;
                if (!resultado) {
                    mostrarDireccion("No encontramos una dirección para este punto. Escríbela a mano en «¿Dónde ocurre el problema?».", false);
                    return;
                }
                const direccion = limpiarDireccion(resultado.formatted_address);
                const campoLibre = direccionInput.value.trim() === "" || direccionInput.value === direccionAuto;
                if (campoLibre) {
                    ponerDireccion(direccion);
                    mostrarDireccion("Dirección del punto: " + direccion + ". La pusimos en «¿Dónde ocurre el problema?»; puedes corregirla o añadir una referencia.", false);
                } else {
                    direccionPendiente = direccion;
                    mostrarDireccion("Dirección del punto: " + direccion, true);
                }
            } catch {
                if (id === consultaDireccion) mostrarDireccion("No se pudo obtener la dirección del punto. Escríbela a mano.", false);
            }
        }

        botonUsarDireccion.addEventListener("click", () => {
            if (!direccionPendiente) return;
            ponerDireccion(direccionPendiente);
            mostrarDireccion("Dirección del punto: " + direccionPendiente + ". La pusimos en «¿Dónde ocurre el problema?».", false);
            direccionInput.focus();
        });

        // origen: "mapa" (clic, arrastre o centro) busca la dirección; "busqueda" y "previo", no.
        function poner(posicion, origen) {
            const punto = aLiteral(posicion);
            if (!marcador) {
                marcador = new G.Marker({ map: mapa, position: punto, gmpDraggable: true, title: "Punto de la incidencia" });
                marcador.addListener("dragend", () => poner(marcador.position, "mapa"));
            } else {
                marcador.position = punto;
            }
            // Se guarda el valor completo del navegador; en pantalla se muestran 6 decimales.
            latInput.value = String(punto.lat);
            lngInput.value = String(punto.lng);
            latTexto.textContent = punto.lat.toFixed(6);
            lngTexto.textContent = punto.lng.toFixed(6);
            panel.classList.add("tiene-punto");
            botonQuitar.hidden = false;
            avisarCambio(latInput);
            if (origen === "mapa") buscarDireccionDelPunto(punto);
            else { consultaDireccion++; mostrarDireccion("", false); }
        }

        function quitar() {
            if (marcador) { marcador.map = null; marcador = null; }
            latInput.value = "";
            lngInput.value = "";
            latTexto.textContent = "—";
            lngTexto.textContent = "—";
            panel.classList.remove("tiene-punto");
            botonQuitar.hidden = true;
            consultaDireccion++;
            mostrarDireccion("", false);
            avisarCambio(latInput);
        }

        mapa.addListener("click", e => { if (e.latLng) poner(e.latLng, "mapa"); });
        // Alternativa al clic (útil con teclado): mover el mapa con las flechas y marcar su centro.
        botonCentro.addEventListener("click", () => poner(mapa.getCenter(), "mapa"));
        botonQuitar.addEventListener("click", quitar);

        if (hayPrevio) poner({ lat: latPrevia, lng: lngPrevia }, "previo");
        else quitar();

        if (direccionInput) await iniciarAutocompletado(direccionInput, mapa, (ubicacion, textoElegido) => {
            ponerDireccion(textoElegido);
            mapa.setCenter(ubicacion);
            mapa.setZoom(17);
            poner(ubicacion, "busqueda");
        });
    }

    // --- Autocompletado de la dirección (Places API (New)) ---
    // Lista accesible (combobox): flechas para moverse, Enter para elegir, Escape para cerrar.
    async function iniciarAutocompletado(input, mapa, alElegir) {
        let places;
        try { places = await google.maps.importLibrary("places"); }
        catch { return; } // sin Places, el campo sigue siendo un texto normal

        const { AutocompleteSuggestion, AutocompleteSessionToken } = places;
        const lista = document.createElement("ul");
        lista.id = input.id + "-sugerencias";
        lista.className = "autocompletar__lista";
        lista.setAttribute("role", "listbox");
        lista.setAttribute("aria-label", "Direcciones sugeridas");
        lista.hidden = true;
        // El campo y su lista van juntos en una caja, para que la lista aparezca justo debajo del campo.
        const caja = document.createElement("div");
        caja.className = "autocompletar";
        input.before(caja);
        caja.append(input, lista);

        input.setAttribute("autocomplete", "off"); // el autocompletado del navegador taparía la lista
        input.setAttribute("role", "combobox");
        input.setAttribute("aria-autocomplete", "list");
        input.setAttribute("aria-controls", lista.id);
        input.setAttribute("aria-expanded", "false");

        let token = new AutocompleteSessionToken(); // agrupa las búsquedas de una misma dirección
        let sugerencias = [];
        let activa = -1;
        let espera = null;
        let consulta = 0;

        function cerrar() {
            lista.hidden = true;
            lista.replaceChildren();
            sugerencias = [];
            activa = -1;
            input.setAttribute("aria-expanded", "false");
            input.removeAttribute("aria-activedescendant");
        }

        function marcarActiva(indice) {
            activa = indice;
            Array.from(lista.children).forEach((li, i) => li.setAttribute("aria-selected", i === indice ? "true" : "false"));
            if (indice >= 0) {
                input.setAttribute("aria-activedescendant", lista.children[indice].id);
                lista.children[indice].scrollIntoView({ block: "nearest" });
            } else {
                input.removeAttribute("aria-activedescendant");
            }
        }

        function mostrar(prediccionesNuevas) {
            sugerencias = prediccionesNuevas;
            lista.replaceChildren();
            if (sugerencias.length === 0) { cerrar(); return; }
            sugerencias.forEach((p, i) => {
                const li = document.createElement("li");
                li.id = lista.id + "-" + i;
                li.className = "autocompletar__opcion";
                li.setAttribute("role", "option");
                li.setAttribute("aria-selected", "false");
                li.append(texto("span", p.mainText ? p.mainText.text : p.text.text, "autocompletar__principal"));
                if (p.secondaryText && p.secondaryText.text) li.append(texto("span", p.secondaryText.text, "autocompletar__secundario"));
                // mousedown (no click): así el campo no pierde el foco antes de elegir.
                li.addEventListener("mousedown", e => { e.preventDefault(); elegir(i); });
                lista.append(li);
            });
            const atribucion = texto("li", "Sugerencias de Google", "autocompletar__atribucion");
            atribucion.setAttribute("role", "presentation");
            lista.append(atribucion);
            lista.hidden = false;
            input.setAttribute("aria-expanded", "true");
            marcarActiva(-1);
        }

        async function buscar() {
            const valor = input.value.trim();
            const id = ++consulta;
            if (valor.length < 3) { cerrar(); return; }
            try {
                const { suggestions } = await AutocompleteSuggestion.fetchAutocompleteSuggestions({
                    input: valor,
                    sessionToken: token,
                    language: "es",
                    region: "pe",
                    includedRegionCodes: ["pe"],
                    locationBias: mapa.getCenter()
                });
                if (id !== consulta || document.activeElement !== input) return;
                mostrar((suggestions || []).map(s => s.placePrediction).filter(Boolean).slice(0, 5));
            } catch {
                if (id === consulta) cerrar();
            }
        }

        async function elegir(indice) {
            const prediccion = sugerencias[indice];
            if (!prediccion) return;
            const textoElegido = prediccion.text.text.replace(/,\s*Per[uú]\s*$/i, "");
            cerrar();
            consulta++;
            try {
                const lugar = prediccion.toPlace();
                await lugar.fetchFields({ fields: ["location"] });
                if (lugar.location) alElegir(lugar.location, textoElegido);
            } catch {
                // Sin coordenadas: se deja al menos el texto elegido.
                input.value = textoElegido;
                input.dispatchEvent(new Event("change", { bubbles: true }));
            }
            token = new AutocompleteSessionToken(); // la sesión termina al elegir un lugar
        }

        input.addEventListener("input", () => {
            clearTimeout(espera);
            espera = setTimeout(buscar, 300); // se espera a que el vecino deje de escribir
        });

        input.addEventListener("keydown", e => {
            if (lista.hidden) return;
            if (e.key === "ArrowDown") { e.preventDefault(); marcarActiva((activa + 1) % sugerencias.length); }
            else if (e.key === "ArrowUp") { e.preventDefault(); marcarActiva(activa <= 0 ? sugerencias.length - 1 : activa - 1); }
            else if (e.key === "Enter" && activa >= 0) { e.preventDefault(); elegir(activa); } // no envía el formulario
            else if (e.key === "Escape") { e.preventDefault(); cerrar(); }
        });

        input.addEventListener("blur", () => { clearTimeout(espera); consulta++; cerrar(); });
    }

    // --- Detalle y Seguimiento: un punto fijo ---
    function iniciarPunto(contenedor) {
        const lat = Number(contenedor.dataset.lat), lng = Number(contenedor.dataset.lng);
        if (!esCoordenada(lat, lng)) return;
        const etiqueta = contenedor.dataset.etiqueta || "Ubicación de la incidencia";
        const mapa = crearMapa(contenedor, { lat, lng }, 17);
        const info = new G.InfoWindow();
        const marcador = new G.Marker({ map: mapa, position: { lat, lng }, title: etiqueta });
        marcador.addListener("click", () => abrirPopup(info, mapa, texto("span", etiqueta, "mapa-popup"), marcador));
    }

    // --- Panel admin: incidencias filtradas ---
    function iniciarIncidencias(contenedor) {
        const idFuente = contenedor.dataset.fuente;
        const urlDetalle = contenedor.dataset.detalle;
        const vacio = document.getElementById(contenedor.dataset.vacio);
        const conteo = document.getElementById(contenedor.dataset.conteo);
        let mapa = null, info = null, marcadores = [], puntosPrevios = -1;

        function leerDatos() {
            // Se vuelve a buscar por id: el tiempo real reemplaza este bloque al refrescar la lista.
            try { return JSON.parse(document.getElementById(idFuente)?.textContent || "{}"); }
            catch { return {}; }
        }

        function popup(p) {
            const caja = document.createElement("div");
            caja.className = "mapa-popup";
            const titulo = document.createElement("a");
            titulo.className = "mapa-popup__titulo";
            titulo.href = urlDetalle + encodeURIComponent(String(p.id));
            titulo.textContent = String(p.titulo || "");
            caja.append(titulo, texto("span", String(p.categoria || ""), "mapa-popup__dato"), texto("span", "Estado: " + String(p.estado || ""), "mapa-popup__dato"));
            return caja;
        }

        function limpiar() {
            marcadores.forEach(m => { m.map = null; });
            marcadores = [];
            if (info) info.close();
        }

        function dibujar() {
            const datos = leerDatos();
            const puntos = (Array.isArray(datos.puntos) ? datos.puntos : [])
                .filter(p => esCoordenada(Number(p.lat), Number(p.lng)));
            const total = Number(datos.total) || 0;

            conteo.textContent = puntos.length === 1
                ? "1 de " + total + " incidencias mostradas tiene ubicación en el mapa."
                : puntos.length + " de " + total + " incidencias mostradas tienen ubicación en el mapa.";

            // Sin puntos: no se muestra un mapa vacío.
            contenedor.hidden = puntos.length === 0;
            vacio.hidden = puntos.length > 0;
            if (puntos.length === 0) { puntosPrevios = 0; limpiar(); return; }

            if (!mapa) {
                mapa = crearMapa(contenedor, { lat: Number(puntos[0].lat), lng: Number(puntos[0].lng) }, 13);
                info = new G.InfoWindow();
            }

            limpiar();
            const limites = new G.LatLngBounds();
            for (const p of puntos) {
                const posicion = { lat: Number(p.lat), lng: Number(p.lng) };
                const marcador = new G.Marker({ map: mapa, position: posicion, title: String(p.titulo || "Incidencia") });
                marcador.addListener("click", () => abrirPopup(info, mapa, popup(p), marcador));
                marcadores.push(marcador);
                limites.extend(posicion);
            }

            // Se ajusta la vista al cargar o si cambia la cantidad de puntos (no en cada refresco).
            if (puntos.length !== puntosPrevios) {
                if (puntos.length === 1) { mapa.setCenter(limites.getCenter()); mapa.setZoom(16); }
                else ajustar(mapa, limites, 30, 16);
            }
            puntosPrevios = puntos.length;
        }

        dibujar();
        // Aviso genérico del tiempo real (realtime.js) después de refrescar la lista con los mismos filtros.
        document.addEventListener("realtime:actualizado", dibujar);
    }

    // --- Panel admin: zonas críticas (clustering K-Means calculado en el servidor) ---
    // Cada zona es un círculo con su número; cada incidencia, un punto del color de su zona.
    function iniciarZonas(contenedor) {
        let datos = {};
        try { datos = JSON.parse(document.getElementById(contenedor.dataset.fuente)?.textContent || "{}"); }
        catch { return; }

        const zonas = (Array.isArray(datos.zonas) ? datos.zonas : []).filter(z => esCoordenada(Number(z.lat), Number(z.lng)));
        const puntos = (Array.isArray(datos.puntos) ? datos.puntos : []).filter(p => esCoordenada(Number(p.lat), Number(p.lng)));
        if (zonas.length === 0) return;

        const mapa = crearMapa(contenedor, { lat: Number(zonas[0].lat), lng: Number(zonas[0].lng) }, 14);
        const info = new G.InfoWindow();
        const circulos = {};
        const limites = new G.LatLngBounds();

        for (const z of zonas) {
            const centro = { lat: Number(z.lat), lng: Number(z.lng) };
            const color = String(z.color || "#2563eb");
            const nombre = "Zona " + Number(z.numero);

            const resumen = () => {
                const caja = document.createElement("div");
                caja.className = "mapa-popup";
                caja.append(
                    texto("strong", nombre, "mapa-popup__titulo"),
                    texto("span", Number(z.total) + " incidencias · " + Number(z.abiertas) + " abiertas", "mapa-popup__dato"),
                    texto("span", "Principal: " + String(z.categoria || ""), "mapa-popup__dato"));
                return caja;
            };

            const circulo = new G.Circle({
                map: mapa, center: centro, radius: Number(z.radio) || 60,
                strokeColor: color, strokeWeight: 2, fillColor: color, fillOpacity: 0.12
            });
            circulo.addListener("click", () => abrirPopup(info, mapa, resumen(), centro));
            circulos[z.numero] = { circulo, centro, resumen };

            // Número de la zona en su centro (se crea con textContent, nunca como HTML).
            const etiqueta = texto("span", String(Number(z.numero)), "zona-marca");
            etiqueta.style.background = color;
            const marca = new G.Marker({ map: mapa, position: centro, content: etiqueta, title: nombre, zIndex: 2 });
            marca.addListener("click", () => abrirPopup(info, mapa, resumen(), marca));

            limites.union(circulo.getBounds());
        }

        for (const p of puntos) {
            const punto = document.createElement("span");
            punto.className = "zona-punto";
            punto.style.background = String(p.color || "#2563eb");
            const marcador = new G.Marker({ map: mapa, position: { lat: Number(p.lat), lng: Number(p.lng) }, content: punto, title: String(p.titulo || "Incidencia"), zIndex: 1 });
            marcador.addListener("click", () => {
                const caja = document.createElement("div");
                caja.className = "mapa-popup";
                const titulo = document.createElement("a");
                titulo.className = "mapa-popup__titulo";
                titulo.href = contenedor.dataset.detalle + encodeURIComponent(String(p.id));
                titulo.textContent = String(p.titulo || "");
                caja.append(titulo, texto("span", String(p.categoria || ""), "mapa-popup__dato"),
                    texto("span", "Estado: " + String(p.estado || "") + " · Zona " + Number(p.zona), "mapa-popup__dato"));
                abrirPopup(info, mapa, caja, marcador);
            });
        }

        ajustar(mapa, limites, 30, 16);

        // "Ver en el mapa" de cada zona de la lista: centra el mapa en esa zona y abre su resumen.
        document.querySelectorAll("[data-zona]").forEach(boton => boton.addEventListener("click", () => {
            const zona = circulos[boton.dataset.zona];
            if (!zona) return;
            contenedor.scrollIntoView({ behavior: "smooth", block: "center" });
            ajustar(mapa, zona.circulo.getBounds(), 40, 17);
            abrirPopup(info, mapa, zona.resumen(), zona.centro);
        }));
    }
})();
