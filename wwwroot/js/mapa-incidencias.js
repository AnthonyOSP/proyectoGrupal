// Mapas de incidencias con Leaflet y teselas de OpenStreetMap (sin API key).
// Cada contenedor indica su modo con data-mapa:
//   "selector"     → formulario Reportar: el vecino marca o arrastra el punto (campos ocultos Latitud/Longitud).
//   "punto"        → Detalle y Seguimiento: un marcador fijo.
//   "incidencias"  → panel admin: marcadores de las incidencias filtradas (datos JSON en la página).
//   "zonas"        → panel admin: zonas críticas agrupadas con K-Means (datos JSON en la página).
// Los textos se muestran con textContent (nunca como HTML) y solo se cargan las teselas visibles.
(function () {
    if (!window.L) return;

    const TESELAS = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
    // Atribución obligatoria de OpenStreetMap: siempre visible (control de Leaflet, abajo a la derecha).
    const ATRIBUCION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

    function crearMapa(contenedor, centro, zoom) {
        // La rueda del mouse solo hace zoom cuando el mapa tiene el foco: así no "atrapa" el scroll de la página.
        const mapa = L.map(contenedor, { center: centro, zoom: zoom, scrollWheelZoom: false });
        L.tileLayer(TESELAS, { maxZoom: 19, attribution: ATRIBUCION }).addTo(mapa);
        mapa.on("focus", () => mapa.scrollWheelZoom.enable());
        mapa.on("blur", () => mapa.scrollWheelZoom.disable());
        return mapa;
    }

    function esCoordenada(lat, lng) {
        return Number.isFinite(lat) && Number.isFinite(lng) && lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180;
    }

    function texto(etiqueta, contenido, clase) {
        const el = document.createElement(etiqueta);
        el.textContent = contenido;
        if (clase) el.className = clase;
        return el;
    }

    // --- Formulario Reportar: elegir el punto ---
    function iniciarSelector(contenedor) {
        const latInput = document.getElementById(contenedor.dataset.latitud);
        const lngInput = document.getElementById(contenedor.dataset.longitud);
        const panel = document.getElementById(contenedor.dataset.panel);
        const latTexto = panel.querySelector("[data-mapa-latitud]");
        const lngTexto = panel.querySelector("[data-mapa-longitud]");
        const botonQuitar = panel.querySelector("[data-mapa-quitar]");
        const botonCentro = panel.querySelector("[data-mapa-centro]");

        // Si el formulario volvió con errores, se recupera el punto que ya se había marcado.
        const latPrevia = parseFloat(latInput.value), lngPrevia = parseFloat(lngInput.value);
        const hayPrevio = esCoordenada(latPrevia, lngPrevia);
        const mapa = crearMapa(contenedor,
            hayPrevio ? [latPrevia, lngPrevia] : [Number(contenedor.dataset.centroLat), Number(contenedor.dataset.centroLng)],
            hayPrevio ? 17 : Number(contenedor.dataset.zoom));
        let marcador = null;

        function avisarCambio() {
            latInput.dispatchEvent(new Event("change", { bubbles: true }));
        }

        function poner(latlng) {
            const punto = latlng.wrap(); // longitud siempre entre -180 y 180
            if (!marcador) {
                marcador = L.marker(punto, { draggable: true, keyboard: true, title: "Punto de la incidencia", alt: "Punto de la incidencia" }).addTo(mapa);
                marcador.on("dragend", () => poner(marcador.getLatLng()));
            } else {
                marcador.setLatLng(punto);
            }
            // Se guarda el valor completo del navegador; en pantalla se muestran 6 decimales.
            latInput.value = String(punto.lat);
            lngInput.value = String(punto.lng);
            latTexto.textContent = punto.lat.toFixed(6);
            lngTexto.textContent = punto.lng.toFixed(6);
            panel.classList.add("tiene-punto");
            botonQuitar.hidden = false;
            avisarCambio();
        }

        function quitar() {
            if (marcador) { mapa.removeLayer(marcador); marcador = null; }
            latInput.value = "";
            lngInput.value = "";
            latTexto.textContent = "—";
            lngTexto.textContent = "—";
            panel.classList.remove("tiene-punto");
            botonQuitar.hidden = true;
            avisarCambio();
        }

        mapa.on("click", e => poner(e.latlng));
        // Alternativa al clic (útil con teclado): mover el mapa con las flechas y marcar su centro.
        botonCentro.addEventListener("click", () => poner(mapa.getCenter()));
        botonQuitar.addEventListener("click", quitar);

        if (hayPrevio) poner(L.latLng(latPrevia, lngPrevia));
        else quitar();
    }

    // --- Detalle y Seguimiento: un punto fijo ---
    function iniciarPunto(contenedor) {
        const lat = Number(contenedor.dataset.lat), lng = Number(contenedor.dataset.lng);
        if (!esCoordenada(lat, lng)) return;
        const etiqueta = contenedor.dataset.etiqueta || "Ubicación de la incidencia";
        const mapa = crearMapa(contenedor, [lat, lng], 17);
        L.marker([lat, lng], { title: etiqueta, alt: etiqueta, keyboard: true })
            .addTo(mapa)
            .bindPopup(texto("span", etiqueta));
    }

    // --- Panel admin: incidencias filtradas ---
    function iniciarIncidencias(contenedor) {
        const idFuente = contenedor.dataset.fuente;
        const urlDetalle = contenedor.dataset.detalle;
        const vacio = document.getElementById(contenedor.dataset.vacio);
        const conteo = document.getElementById(contenedor.dataset.conteo);
        let mapa = null, capa = null, puntosPrevios = -1;

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
            if (puntos.length === 0) { puntosPrevios = 0; if (capa) capa.clearLayers(); return; }

            if (!mapa) {
                mapa = crearMapa(contenedor, [Number(puntos[0].lat), Number(puntos[0].lng)], 13);
                capa = L.layerGroup().addTo(mapa);
            } else {
                mapa.invalidateSize();
            }

            capa.clearLayers();
            const limites = [];
            for (const p of puntos) {
                const lat = Number(p.lat), lng = Number(p.lng);
                const nombre = String(p.titulo || "Incidencia");
                L.marker([lat, lng], { title: nombre, alt: nombre, keyboard: true })
                    .bindPopup(() => popup(p))
                    .addTo(capa);
                limites.push([lat, lng]);
            }

            // Se ajusta la vista al cargar o si cambia la cantidad de puntos (no en cada refresco).
            if (puntos.length !== puntosPrevios) {
                if (limites.length === 1) mapa.setView(limites[0], 16);
                else mapa.fitBounds(limites, { padding: [30, 30], maxZoom: 16 });
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

        const mapa = crearMapa(contenedor, [Number(zonas[0].lat), Number(zonas[0].lng)], 14);
        const circulos = {};
        const limites = [];

        for (const z of zonas) {
            const centro = [Number(z.lat), Number(z.lng)];
            const color = String(z.color || "#2563eb");
            const nombre = "Zona " + Number(z.numero);

            const caja = document.createElement("div");
            caja.className = "mapa-popup";
            caja.append(
                texto("strong", nombre, "mapa-popup__titulo"),
                texto("span", Number(z.total) + " incidencias · " + Number(z.abiertas) + " abiertas", "mapa-popup__dato"),
                texto("span", "Principal: " + String(z.categoria || ""), "mapa-popup__dato"));

            circulos[z.numero] = L.circle(centro, { radius: Number(z.radio) || 60, color: color, weight: 2, fillColor: color, fillOpacity: 0.12 })
                .bindPopup(caja)
                .addTo(mapa);

            // Número de la zona en su centro (se crea con textContent, nunca como HTML).
            const etiqueta = texto("span", String(Number(z.numero)), "zona-marca");
            etiqueta.style.background = color;
            L.marker(centro, { icon: L.divIcon({ html: etiqueta, className: "zona-marca__icono", iconSize: [28, 28] }), title: nombre, alt: nombre, keyboard: true })
                .bindPopup(caja)
                .addTo(mapa);

            limites.push(circulos[z.numero].getBounds());
        }

        for (const p of puntos) {
            L.circleMarker([Number(p.lat), Number(p.lng)], { radius: 6, color: "#ffffff", weight: 2, fillColor: String(p.color || "#2563eb"), fillOpacity: 1 })
                .bindPopup(() => {
                    const caja = document.createElement("div");
                    caja.className = "mapa-popup";
                    const titulo = document.createElement("a");
                    titulo.className = "mapa-popup__titulo";
                    titulo.href = contenedor.dataset.detalle + encodeURIComponent(String(p.id));
                    titulo.textContent = String(p.titulo || "");
                    caja.append(titulo, texto("span", String(p.categoria || ""), "mapa-popup__dato"),
                        texto("span", "Estado: " + String(p.estado || "") + " · Zona " + Number(p.zona), "mapa-popup__dato"));
                    return caja;
                })
                .addTo(mapa);
        }

        mapa.fitBounds(limites.reduce((a, b) => a.extend(b)), { padding: [30, 30], maxZoom: 16 });

        // "Ver en el mapa" de cada zona de la lista: centra el mapa en esa zona y abre su resumen.
        document.querySelectorAll("[data-zona]").forEach(boton => boton.addEventListener("click", () => {
            const circulo = circulos[boton.dataset.zona];
            if (!circulo) return;
            contenedor.scrollIntoView({ behavior: "smooth", block: "center" });
            mapa.fitBounds(circulo.getBounds(), { padding: [40, 40], maxZoom: 17 });
            circulo.openPopup();
        }));
    }

    document.querySelectorAll("[data-mapa]").forEach(contenedor => {
        const modo = contenedor.dataset.mapa;
        if (modo === "selector") iniciarSelector(contenedor);
        else if (modo === "punto") iniciarPunto(contenedor);
        else if (modo === "incidencias") iniciarIncidencias(contenedor);
        else if (modo === "zonas") iniciarZonas(contenedor);
    });
})();
