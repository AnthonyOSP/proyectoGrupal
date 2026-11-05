// Inicio de sesión (Views/Account/Login.cshtml).
// Todo es una mejora: sin JavaScript el formulario funciona igual.
(function () {
    "use strict";

    var raiz = document.querySelector("[data-av-login]");
    if (!raiz) {
        return;
    }

    // 1. Resalta por turnos cada reporte de ejemplo y su pin en el mapa.
    //    Si la persona pidió reducir el movimiento en su sistema, se queda quieto.
    var sinMovimiento = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var total = 3;
    var activo = 0;

    if (!sinMovimiento) {
        setInterval(function () {
            activo = (activo + 1) % total;
            raiz.querySelectorAll("[data-av-item]").forEach(function (elemento) {
                elemento.classList.toggle("es-activo", elemento.getAttribute("data-av-item") === String(activo));
            });
        }, 3200);
    }

    // 2. Mostrar u ocultar la contraseña.
    var ojo = raiz.querySelector("[data-av-ojo]");
    var clave = ojo && document.getElementById(ojo.getAttribute("aria-controls"));
    if (ojo && clave) {
        ojo.hidden = false;
        ojo.addEventListener("click", function () {
            var mostrar = clave.type === "password";
            clave.type = mostrar ? "text" : "password";
            ojo.setAttribute("aria-pressed", mostrar ? "true" : "false");
            ojo.setAttribute("aria-label", mostrar ? "Ocultar contraseña" : "Mostrar contraseña");
            ojo.querySelector("[data-av-ojo-mostrar]").hidden = mostrar;
            ojo.querySelector("[data-av-ojo-ocultar]").hidden = !mostrar;
        });
    }

    // 3. Evita envíos dobles y muestra "Ingresando…" (solo si la validación del cliente pasó).
    var formulario = raiz.querySelector("[data-av-form]");
    var boton = raiz.querySelector("[data-av-enviar]");
    if (formulario && boton) {
        formulario.addEventListener("submit", function () {
            var $ = window.jQuery;
            if ($ && $.fn.valid && !$(formulario).valid()) {
                return;
            }

            // Se desactiva después de que el navegador tomó los datos del formulario.
            setTimeout(function () {
                boton.disabled = true;
                boton.querySelector(".av-boton__normal").hidden = true;
                boton.querySelector(".av-boton__cargando").hidden = false;
            });
        });

        // Al volver con el botón "Atrás" del navegador, el botón queda como al inicio.
        window.addEventListener("pageshow", function () {
            boton.disabled = false;
            boton.querySelector(".av-boton__normal").hidden = false;
            boton.querySelector(".av-boton__cargando").hidden = true;
        });
    }
})();
