(() => {
    const dialog = document.querySelector("[data-evidence-dialog]");
    const image = dialog?.querySelector("[data-evidence-dialog-image]");
    if (!dialog || !image) return;
    document.querySelectorAll("[data-evidence-image]").forEach(button => {
        button.addEventListener("click", () => {
            image.src = button.dataset.evidenceImage;
            image.alt = button.dataset.evidenceAlt ?? "Enlarged approved evidence";
            dialog.showModal();
        });
    });
    dialog.querySelector("[data-evidence-close]")?.addEventListener("click", () => dialog.close());
    dialog.addEventListener("click", event => { if (event.target === dialog) dialog.close(); });
})();
