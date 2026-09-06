(() => {
    const dialogs = [...document.querySelectorAll("[data-evidence-dialog]")].filter(dialog => dialog.querySelector("[data-evidence-dialog-image]"));
    if (dialogs.length === 0) return;
    const clamp = (value, minimum, maximum) => Math.min(maximum, Math.max(minimum, value));
    const viewers = dialogs.map(dialog => {
        const image = dialog.querySelector("[data-evidence-dialog-image]");
        const viewport = dialog.querySelector("[data-evidence-viewport]") || image.parentElement;
        let scale = 1, offsetX = 0, offsetY = 0;
        const pointers = new Map();
        let pinchDistance = 0, pinchScale = 1, dragPoint = null;
        const bounds = () => ({
            x: Math.max(0, ((image.offsetWidth * scale) - viewport.clientWidth) / 2 + 16),
            y: Math.max(0, ((image.offsetHeight * scale) - viewport.clientHeight) / 2 + 16)
        });
        const applyTransform = () => {
            const limit = bounds();
            offsetX = clamp(offsetX, -limit.x, limit.x);
            offsetY = clamp(offsetY, -limit.y, limit.y);
            image.style.transform = scale === 1 && offsetX === 0 && offsetY === 0
                ? ""
                : `translate3d(${offsetX}px, ${offsetY}px, 0) scale(${scale})`;
            image.classList.toggle("is-zoomed", scale > 1);
        };
        const setScale = value => {
            scale = clamp(value, 1, 4);
            if (scale === 1) { offsetX = 0; offsetY = 0; }
            applyTransform();
        };
        const resetTransform = () => {
            scale = 1;
            offsetX = 0;
            offsetY = 0;
            applyTransform();
        };
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
            resetTransform();
            image.removeAttribute("src");
            image.alt = "";
            clearMetadata();
        };
        const open = trigger => {
            resetTransform();
            image.src = trigger.dataset.evidenceImage;
            image.alt = trigger.dataset.evidenceAlt ?? dialog.dataset.enlargedEvidence ?? "";
            updateMetadata(trigger);
            if (!dialog.open) dialog.showModal();
        };
        const distance = () => {
            const points = [...pointers.values()];
            return points.length < 2 ? 0 : Math.hypot(points[0].x - points[1].x, points[0].y - points[1].y);
        };
        dialog.addEventListener("close", clearImage);
        if (dialog.open) dialog.close();
        dialog.querySelector("[data-evidence-close]")?.addEventListener("click", () => dialog.close());
        dialog.querySelector("[data-evidence-zoom-in]")?.addEventListener("click", () => setScale(scale + 0.5));
        dialog.querySelector("[data-evidence-zoom-out]")?.addEventListener("click", () => setScale(scale - 0.5));
        dialog.querySelector("[data-evidence-zoom-reset]")?.addEventListener("click", resetTransform);
        dialog.addEventListener("click", event => { if (event.target === dialog) dialog.close(); });
        image.addEventListener("load", applyTransform);
        image.addEventListener("keydown", event => {
            if (event.key === "+" || event.key === "=") { setScale(scale + 0.5); event.preventDefault(); }
            else if (event.key === "-" || event.key === "_") { setScale(scale - 0.5); event.preventDefault(); }
            else if (event.key === "0") { resetTransform(); event.preventDefault(); }
            else if (event.key === "ArrowLeft") { offsetX -= 40; applyTransform(); event.preventDefault(); }
            else if (event.key === "ArrowRight") { offsetX += 40; applyTransform(); event.preventDefault(); }
            else if (event.key === "ArrowUp") { offsetY -= 40; applyTransform(); event.preventDefault(); }
            else if (event.key === "ArrowDown") { offsetY += 40; applyTransform(); event.preventDefault(); }
        });
        viewport.addEventListener("wheel", event => {
            setScale(scale + (event.deltaY < 0 ? 0.25 : -0.25));
            event.preventDefault();
        }, { passive: false });
        viewport.addEventListener("pointerdown", event => {
            pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
            viewport.setPointerCapture?.(event.pointerId);
            if (pointers.size === 2) { pinchDistance = distance(); pinchScale = scale; }
            else dragPoint = { x: event.clientX, y: event.clientY };
            event.preventDefault();
        });
        viewport.addEventListener("pointermove", event => {
            if (!pointers.has(event.pointerId)) return;
            pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
            if (pointers.size > 1) setScale(pinchScale * (distance() / Math.max(1, pinchDistance)));
            else if (scale > 1 && dragPoint) {
                offsetX += event.clientX - dragPoint.x;
                offsetY += event.clientY - dragPoint.y;
                dragPoint = { x: event.clientX, y: event.clientY };
                applyTransform();
            }
            event.preventDefault();
        });
        const endPointer = event => { pointers.delete(event.pointerId); dragPoint = null; };
        viewport.addEventListener("pointerup", endPointer);
        viewport.addEventListener("pointercancel", endPointer);
        return { open };
    });
    document.addEventListener("click", event => {
        const trigger = event.target.closest?.("[data-evidence-image]");
        if (!trigger) return;
        const viewer = viewers[0];
        if (!viewer) return;
        viewer.open(trigger);
        event.preventDefault();
    });
})();
