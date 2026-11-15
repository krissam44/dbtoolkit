
export function initialize(headerElement) {

    let dialogElement = headerElement.closest(".mud-dialog");

    if (!dialogElement) {
        console.error("Conteneur MudDialog introuvable.");
        return { dispose: () => { } };
    }

    let startX = 0;
    let startY = 0;
    let initialX = 0;
    let initialY = 0;

    function onMouseDown(event) {

        if (event.target.closest("button"))
            return;

        event.preventDefault();

        startX = event.clientX;
        startY = event.clientY;

        const rect = dialogElement.getBoundingClientRect();

        initialX = rect.left;
        initialY = rect.top;

        dialogElement.style.position = "fixed";
        dialogElement.style.margin = "0";
        dialogElement.style.transform = "none";
        dialogElement.style.left = `${initialX}px`;
        dialogElement.style.top = `${initialY}px`;

        document.addEventListener("mousemove", onMouseMove);
        document.addEventListener("mouseup", onMouseUp);
    }

    function onMouseMove(event) {

        const deltaX = event.clientX - startX;
        const deltaY = event.clientY - startY;

        const maxX = window.innerWidth - dialogElement.offsetWidth;
        const maxY = window.innerHeight - dialogElement.offsetHeight;

        const newX = Math.max(0, Math.min(initialX + deltaX, maxX));
        const newY = Math.max(0, Math.min(initialY + deltaY, maxY));

        dialogElement.style.left = `${newX}px`;
        dialogElement.style.top = `${newY}px`;
    }

    function onMouseUp() {
        document.removeEventListener("mousemove", onMouseMove);
        document.removeEventListener("mouseup", onMouseUp);
    }

    headerElement.style.cursor = "move";
    headerElement.addEventListener("mousedown", onMouseDown);

    return {
        dispose: () => {
            headerElement.removeEventListener("mousedown", onMouseDown);
            document.removeEventListener("mousemove", onMouseMove);
            document.removeEventListener("mouseup", onMouseUp);
        }
    };
}
