// Páginas de cuenta a pantalla completa: inicio de sesión y registro (Views/Account/Login.cshtml y Register.cshtml).
// Todo es una mejora: sin JavaScript los formularios funcionan igual.
(function () {
    "use strict";

    var raiz = document.querySelector("[data-av-auth]");
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

    // 2. Mostrar u ocultar cada contraseña (el registro tiene dos campos).
    raiz.querySelectorAll("[data-av-ojo]").forEach(function (ojo) {
        var clave = document.getElementById(ojo.getAttribute("aria-controls"));
        if (!clave) {
            return;
        }

        ojo.hidden = false;
        ojo.addEventListener("click", function () {
            var mostrar = clave.type === "password";
            clave.type = mostrar ? "text" : "password";
            ojo.setAttribute("aria-pressed", mostrar ? "true" : "false");
            ojo.setAttribute("aria-label", mostrar ? "Ocultar contraseña" : "Mostrar contraseña");
            ojo.querySelector("[data-av-ojo-mostrar]").hidden = mostrar;
            ojo.querySelector("[data-av-ojo-ocultar]").hidden = !mostrar;
        });
    });

    // 3. Evita envíos dobles y muestra el estado de carga (solo si la validación del cliente pasó).
    var formulario = raiz.querySelector("[data-av-form]");
    var boton = raiz.querySelector("[data-av-enviar]");
    if (formulario && boton) {
        var normal = boton.querySelector(".av-boton__normal");
        var cargando = boton.querySelector(".av-boton__cargando");

        formulario.addEventListener("submit", function () {
            var $ = window.jQuery;
            if ($ && $.fn.valid && !$(formulario).valid()) {
                return;
            }

            // Se desactiva después de que el navegador tomó los datos del formulario.
            setTimeout(function () {
                boton.disabled = true;
                normal.hidden = true;
                cargando.hidden = false;
            });
        });

        // Al volver con el botón "Atrás" del navegador, el botón queda como al inicio.
        window.addEventListener("pageshow", function () {
            boton.disabled = false;
            normal.hidden = false;
            cargando.hidden = true;
        });
    }
})();
