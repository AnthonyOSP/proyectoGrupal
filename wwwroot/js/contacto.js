// Comportamiento del formulario "Contáctanos".
// JavaScript sencillo, sin librerías adicionales. Todo es una ayuda visual:
// el servidor vuelve a validar cada dato y decide qué se guarda.
(function () {
    // 1. Llevar el foco al mensaje de éxito o al resumen de errores (útil para lectores de pantalla).
    const aviso = document.getElementById("alertaExito") || document.getElementById("resumenErrores");
    if (aviso) aviso.focus();

    const form = document.getElementById("formContacto");
    if (!form) return;

    // 2. Contador de caracteres del mensaje: "125 / 1000 caracteres" (igual que en Reportar).
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

    // 3. Evitar envíos múltiples: después del primer envío válido el botón queda deshabilitado.
    const boton = document.getElementById("btnEnviar");
    const textoBoton = boton.querySelector(".btn-texto");
    const textoOriginal = textoBoton.textContent;
    let enviando = false;

    form.addEventListener("submit", e => {
        if (enviando) {
            e.preventDefault();
            return;
        }
        if (window.jQuery && jQuery(form).valid && !jQuery(form).valid()) {
            const primerError = form.querySelector(".input-validation-error");
            if (primerError) primerError.focus();
            return;
        }
        enviando = true;
        boton.classList.add("is-loading");
        boton.disabled = true;
        boton.setAttribute("aria-busy", "true");
        textoBoton.textContent = "Enviando...";
    });

    // Si se vuelve con el botón "Atrás", el navegador puede restaurar el botón deshabilitado.
    window.addEventListener("pageshow", e => {
        if (!e.persisted) return;
        enviando = false;
        boton.classList.remove("is-loading");
        boton.disabled = false;
        boton.removeAttribute("aria-busy");
        textoBoton.textContent = textoOriginal;
    });
})();
