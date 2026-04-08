export function shouldAlignRight(element, popoverWidth) {
    const rect = element.getBoundingClientRect();
    return (window.innerWidth - rect.left) < (popoverWidth + 16);
}
