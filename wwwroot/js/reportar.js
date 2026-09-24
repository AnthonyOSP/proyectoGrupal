// Comportamiento del formulario "Reportar incidencia".
// JavaScript sencillo, sin librerías adicionales.
(function () {
    const form = document.getElementById("formReporte");
    if (!form) return;

    const TAMANO_MAXIMO = 5 * 1024 * 1024; // 5 MB

    // 1. Llevar el foco al resumen de errores (útil para lectores de pantalla).
    const aviso = document.getElementById("resumenErrores");
    if (aviso) aviso.focus();

    // 2. Contador de caracteres de la descripción.
    const descripcion = form.querySelector("[data-contador]");
    if (descripcion) {
        const contador = document.getElementById(descripcion.dataset.contador);
        const max = descripcion.getAttribute("maxlength");
        const actualizar = () => { contador.textContent = descripcion.value.length + "/" + max; };
        descripcion.addEventListener("input", actualizar);
        actualizar();
    }

    // 3. Selector de fotografía con vista previa.
    const input = document.getElementById("Foto");
    const drop = document.getElementById("fotoDrop");
    const preview = document.getElementById("fotoPreview");
    const previewImg = document.getElementById("fotoPreviewImg");
    const previewNombre = document.getElementById("fotoPreviewNombre");
    const previewPeso = document.getElementById("fotoPreviewPeso");
    const quitar = document.getElementById("fotoQuitar");
    const error = document.getElementById("fotoError");

    function mostrarError(mensaje) {
        error.textContent = mensaje;
        error.className = mensaje ? "field-validation-error" : "field-validation-valid";
    }

    function formatearPeso(bytes) {
        return bytes < 1024 * 1024
            ? Math.round(bytes / 1024) + " KB"
            : (bytes / (1024 * 1024)).toFixed(1) + " MB";
    }

    function limpiarFoto() {
        input.value = "";
        previewImg.removeAttribute("src");
        preview.hidden = true;
        drop.hidden = false;
    }

    function mostrarFoto(archivo) {
        mostrarError("");
        if (!archivo) return;

        if (!archivo.type.startsWith("image/")) {
            limpiarFoto();
            mostrarError("El archivo debe ser una imagen (JPG o PNG).");
            return;
        }
        if (archivo.size > TAMANO_MAXIMO) {
            limpiarFoto();
            mostrarError("La imagen pesa más de 5 MB. Elige una más liviana.");
            return;
        }

        previewImg.src = URL.createObjectURL(archivo);
        previewNombre.textContent = archivo.name;
        previewPeso.textContent = formatearPeso(archivo.size);
        preview.hidden = false;
        drop.hidden = true;
        quitar.focus();
    }

    input.addEventListener("change", () => mostrarFoto(input.files[0]));

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

    // 4. Estado "Enviando..." del botón para evitar doble envío.
    const boton = document.getElementById("btnEnviar");
    form.addEventListener("submit", () => {
        // Si la validación del navegador (jQuery Validate) encontró errores, no mostramos la carga.
        if (window.jQuery && jQuery(form).valid && !jQuery(form).valid()) {
            const primerError = form.querySelector(".input-validation-error");
            if (primerError) primerError.focus();
            return;
        }
        boton.classList.add("is-loading");
        boton.disabled = true;
        boton.querySelector(".btn-texto").textContent = "Enviando...";
    });
})();
