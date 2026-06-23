window.getBoundingClientRect = (element) => {
    if (element) {
        return element.getBoundingClientRect();
    }
    return { top: 0, height: 0 };
};

window.elementRef = {
    setPointerCapture: (element, pointerId) => {
        if (element && element.setPointerCapture) {
            element.setPointerCapture(pointerId);
        }
    },
    releasePointerCapture: (element, pointerId) => {
        if (element && element.releasePointerCapture) {
            element.releasePointerCapture(pointerId);
        }
    }
};
