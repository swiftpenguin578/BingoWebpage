(() => {
    const dialog = document.querySelector("[data-evidence-dialog]");
    const image = dialog?.querySelector("[data-evidence-dialog-image]");
    if (!dialog || !image) return;
    const clearMetadata = () => {
        dialog.querySelector("[data-evidence-dialog-drop]")?.replaceChildren(document.createTextNode(dialog.dataset.evidenceEmpty ?? ""));
        dialog.querySelector("[data-evidence-dialog-player]")?.replaceChildren(document.createTextNode("—"));
        dialog.querySelector("[data-evidence-dialog-team]")?.replaceChildren(document.createTextNode("—"));
        dialog.querySelector("[data-evidence-dialog-source]")?.replaceChildren(document.createTextNode(dialog.dataset.evidenceDefaultSource ?? ""));
    };
    const updateMetadata = trigger => {
        dialog.querySelector("[data-evidence-dialog-drop]")?.replaceChildren(document.createTextNode(trigger.dataset.evidenceDrop ?? trigger.dataset.evidenceAlt ?? dialog.dataset.evidenceEmpty ?? ""));
        dialog.querySelector("[data-evidence-dialog-player]")?.replaceChildren(document.createTextNode(trigger.dataset.evidencePlayer ?? "—"));
        dialog.querySelector("[data-evidence-dialog-team]")?.replaceChildren(document.createTextNode(trigger.dataset.evidenceTeam ?? "—"));
        dialog.querySelector("[data-evidence-dialog-source]")?.replaceChildren(document.createTextNode(trigger.dataset.evidenceSource ?? dialog.dataset.evidenceDefaultSource ?? ""));
    };
    const clearImage = () => {
        image.removeAttribute("src");
        image.alt = "";
        clearMetadata();
    };
    dialog.addEventListener("close", clearImage);
    if (dialog.open) dialog.close();
    document.addEventListener("click", event => {
        const trigger = event.target.closest?.("[data-evidence-image]");
        if (!trigger) return;
        image.src = trigger.dataset.evidenceImage;
        image.alt = trigger.dataset.evidenceAlt ?? dialog.dataset.enlargedEvidence ?? "";
        updateMetadata(trigger);
        if (!dialog.open) dialog.showModal();
        event.preventDefault();
    });
    dialog.querySelector("[data-evidence-close]")?.addEventListener("click", () => dialog.close());
    dialog.addEventListener("click", event => { if (event.target === dialog) dialog.close(); });
})();
