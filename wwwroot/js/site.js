// Cambio entre modo claro y modo oscuro.
// El tema inicial lo aplica Views/Shared/_TemaScript.cshtml; aquí solo se maneja el botón.
(function () {
    const raiz = document.documentElement;
    const botones = document.querySelectorAll("[data-cambiar-tema]");
    const sistemaOscuro = window.matchMedia("(prefers-color-scheme: dark)");

    function aplicar(tema) {
        raiz.setAttribute("data-theme", tema);
        raiz.setAttribute("data-bs-theme", tema);

        const oscuro = tema === "dark";
        botones.forEach(boton => {
            boton.setAttribute("aria-label", oscuro ? "Activar modo claro" : "Activar modo oscuro");
            boton.setAttribute("aria-pressed", oscuro ? "true" : "false");
        });
    }

    function temaGuardado() {
        try { return localStorage.getItem("tema"); } catch (e) { return null; }
    }

    // Deja los botones con el texto correcto según el tema que ya se aplicó.
    aplicar(raiz.getAttribute("data-theme") || "light");

    botones.forEach(boton => boton.addEventListener("click", () => {
        const nuevo = raiz.getAttribute("data-theme") === "dark" ? "light" : "dark";
        aplicar(nuevo);
        try { localStorage.setItem("tema", nuevo); } catch (e) { }
    }));

    // Si el usuario nunca eligió un tema, se sigue el del sistema operativo al cambiarlo.
    sistemaOscuro.addEventListener("change", e => {
        if (!temaGuardado()) {
            aplicar(e.matches ? "dark" : "light");
        }
    });
})();
