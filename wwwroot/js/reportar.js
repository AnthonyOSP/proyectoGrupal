// Comportamiento del formulario "Reportar incidencia".
// JavaScript sencillo, sin librerías adicionales. Todo es una ayuda visual:
// el servidor vuelve a validar cada dato y decide qué se guarda.
(function () {
    const form = document.getElementById("formReporte");
    if (!form) return;

    const TAMANO_MAXIMO = 5 * 1024 * 1024; // 5 MB (igual que FotoIncidenciaService)
    const TIPOS_PERMITIDOS = ["image/jpeg", "image/png"];
    const LARGO_RESUMEN = 160;             // igual que Resumir() en Reportar.cshtml
    const SIN_COMPLETAR = "Sin completar";

    // 1. Llevar el foco al resumen de errores (útil para lectores de pantalla).
    const aviso = document.getElementById("resumenErrores");
    if (aviso) aviso.focus();

    // 2. Contadores de caracteres: "125 / 1000 caracteres".
    //    Se avisa (con texto, no solo con color) si falta llegar al mínimo o se está cerca del máximo.
    form.querySelectorAll("[data-contador]").forEach(campo => {
        const contador = document.getElementById(campo.dataset.contador);
        const maximo = Number(campo.getAttribute("maxlength"));
        const minimo = Number(contador.dataset.minimo || 0);

        const actualizar = () => {
            const largo = campo.value.trim().length;
            let texto = largo + " / " + maximo + " caracteres";
            if (largo > 0 && largo < minimo) texto += " · faltan " + (minimo - largo);
            contador.textContent = texto;
            contador.classList.toggle("is-alerta", (largo > 0 && largo < minimo) || largo >= maximo * 0.9);
        };
        campo.addEventListener("input", actualizar);
        actualizar();
    });

    // 3. Resumen antes de enviar. Se usa textContent (nunca innerHTML): lo escrito se muestra como texto.
    function ponerResumen(nombre, valor) {
        const destino = form.querySelector('[data-resumen="' + nombre + '"]');
        if (!destino) return;
        const vacio = !valor;
        const textoVacio = { Foto: "Sin fotografía", Mapa: "Sin punto en el mapa" }[nombre] || SIN_COMPLETAR;
        destino.textContent = vacio ? textoVacio : valor;
        destino.classList.toggle("is-vacio", vacio);
    }

    function unaLinea(texto) {
        return texto.trim().replace(/\s+/g, " ");
    }

    function resumir(texto) {
        texto = texto.trim();
        return texto.length <= LARGO_RESUMEN ? texto : texto.slice(0, LARGO_RESUMEN).trimEnd() + "…";
    }

    function actualizarResumen() {
        ponerResumen("Titulo", unaLinea(form.elements["Titulo"].value));
        ponerResumen("Ubicacion", unaLinea(form.elements["Ubicacion"].value));
        ponerResumen("Descripcion", resumir(form.elements["Descripcion"].value));
        const categoria = form.querySelector('input[name="Categoria"]:checked');
        ponerResumen("Categoria", categoria ? categoria.value : "");

        // Punto del mapa (lo llena mapa-incidencias.js en campos ocultos).
        const lat = parseFloat(form.elements["Latitud"]?.value), lng = parseFloat(form.elements["Longitud"]?.value);
        ponerResumen("Mapa", Number.isFinite(lat) && Number.isFinite(lng) ? lat.toFixed(6) + ", " + lng.toFixed(6) : "");
    }

    form.querySelectorAll("[data-resumen-origen]").forEach(campo => {
        campo.addEventListener("input", actualizarResumen);
        campo.addEventListener("change", actualizarResumen);
    });
    actualizarResumen();

    // 4. Selector de fotografía con vista previa, "Cambiar" y "Quitar".
    //    La vista previa usa una URL local del navegador: la imagen no se sube hasta enviar el formulario.
    const input = document.getElementById("Foto");
    const drop = document.getElementById("fotoDrop");
    const preview = document.getElementById("fotoPreview");
    const previewImg = document.getElementById("fotoPreviewImg");
    const previewNombre = document.getElementById("fotoPreviewNombre");
    const previewPeso = document.getElementById("fotoPreviewPeso");
    const cambiar = document.getElementById("fotoCambiar");
    const quitar = document.getElementById("fotoQuitar");
    const error = document.getElementById("fotoError");
    let urlVistaPrevia = null;

    function mostrarError(mensaje) {
        error.textContent = mensaje;
        error.className = mensaje ? "field-validation-error" : "field-validation-valid";
    }

    function formatearPeso(bytes) {
        return bytes < 1024 * 1024
            ? Math.round(bytes / 1024) + " KB"
            : (bytes / (1024 * 1024)).toFixed(1) + " MB";
    }

    function liberarVistaPrevia() {
        if (urlVistaPrevia) URL.revokeObjectURL(urlVistaPrevia);
        urlVistaPrevia = null;
    }

    function limpiarFoto() {
        input.value = "";
        liberarVistaPrevia();
        previewImg.removeAttribute("src");
        preview.hidden = true;
        drop.hidden = false;
        ponerResumen("Foto", "");
    }

    function mostrarFoto(archivo) {
        mostrarError("");
        if (!archivo) {
            limpiarFoto();
            return;
        }

        if (!TIPOS_PERMITIDOS.includes(archivo.type)) {
            limpiarFoto();
            mostrarError("Solo se permiten fotografías JPG o PNG.");
            return;
        }
        if (archivo.size > TAMANO_MAXIMO) {
            limpiarFoto();
            mostrarError("La imagen pesa más de 5 MB. Elige una más liviana.");
            return;
        }

        liberarVistaPrevia();
        urlVistaPrevia = URL.createObjectURL(archivo);
        previewImg.src = urlVistaPrevia;
        previewNombre.textContent = archivo.name;
        previewPeso.textContent = formatearPeso(archivo.size);
        preview.hidden = false;
        drop.hidden = true;
        ponerResumen("Foto", archivo.name + " (" + formatearPeso(archivo.size) + ")");
        cambiar.focus();
    }

    input.addEventListener("change", () => mostrarFoto(input.files[0]));

    // "Cambiar" abre de nuevo el selector de archivos del sistema.
    cambiar.addEventListener("click", () => input.click());

    quitar.addEventListener("click", () => {
        limpiarFoto();
        input.focus();
    });

    // Arrastrar y soltar (en escritorio). También se puede hacer clic o usar el teclado.
    ["dragenter", "dragover"].forEach(evento =>
        drop.addEventListener(evento, e => { e.preventDefault(); drop.classList.add("is-dragover"); }));
    ["dragleave", "drop"].forEach(evento =>
        drop.addEventListener(evento, e => { e.preventDefault(); drop.classList.remove("is-dragover"); }));
    drop.addEventListener("drop", e => {
        const archivo = e.dataTransfer.files[0];
        if (!archivo) return;
        const lista = new DataTransfer();
        lista.items.add(archivo);
        input.files = lista.files;
        mostrarFoto(archivo);
    });

    // 5. Evitar envíos múltiples: después del primer envío válido, el botón queda deshabilitado
    //    y los clics extra se ignoran. Si la validación del navegador encuentra errores, NO se bloquea nada.
    const boton = document.getElementById("btnEnviar");
    const textoBoton = boton.querySelector(".btn-texto");
    const textoOriginal = textoBoton.textContent;
    let enviando = false;

    form.addEventListener("submit", e => {
        if (enviando) {
            e.preventDefault();
            return;
        }
        // Si la validación del navegador (jQuery Validate) encontró errores, se lleva el foco al primero.
        if (window.jQuery && jQuery(form).valid && !jQuery(form).valid()) {
            const primerError = form.querySelector(".input-validation-error");
            if (primerError) primerError.focus();
            return;
        }
        enviando = true;
        boton.classList.add("is-loading");
        boton.disabled = true;
        boton.setAttribute("aria-busy", "true");
        // Si hay foto, el servidor la procesa (detección de rostros), así que puede tardar unos segundos.
        textoBoton.textContent = input.files.length > 0 ? "Protegiendo foto y enviando..." : "Enviando...";
    });

    // Si el usuario vuelve a esta página con el botón "Atrás", el navegador puede restaurarla
    // con el botón deshabilitado: se reactiva.
    window.addEventListener("pageshow", e => {
        if (!e.persisted) return;
        enviando = false;
        boton.classList.remove("is-loading");
        boton.disabled = false;
        boton.removeAttribute("aria-busy");
        textoBoton.textContent = textoOriginal;
    });
})();
