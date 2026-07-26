(() => {
    const dialog = document.querySelector("[data-evidence-dialog]");
    const image = dialog?.querySelector("[data-evidence-dialog-image]");
    if (!dialog || !image) return;
    document.addEventListener("click", event => {
        const button = event.target.closest?.("[data-evidence-image]");
        if (!button) return;
        image.src = button.dataset.evidenceImage;
        image.alt = button.dataset.evidenceAlt ?? dialog.dataset.enlargedEvidence ?? "Enlarged approved evidence";
        dialog.showModal();
    });
    dialog.querySelector("[data-evidence-close]")?.addEventListener("click", () => dialog.close());
    dialog.addEventListener("click", event => { if (event.target === dialog) dialog.close(); });
})();
