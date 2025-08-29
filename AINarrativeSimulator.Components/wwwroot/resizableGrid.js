export function initResizableGrid(gridEl) {
    if (!gridEl) return;
    attachHandlers(gridEl);
    setupLayoutReset(gridEl);
}

export function reinitGrid(gridEl, isTwoColumn) {
    if (!gridEl) return;
    // Normalize widths so remaining columns share space equally
    if (isTwoColumn) {
        setVar(gridEl, '--col1', '1fr');
        setVar(gridEl, '--col2', '1fr');
    } else {
        setVar(gridEl, '--col1', '1fr');
        setVar(gridEl, '--col2', '1fr');
        setVar(gridEl, '--col3', '1fr');
    }
    attachHandlers(gridEl);
}

function setupLayoutReset(gridEl) {
    const mq = window.matchMedia('(max-width: 1023.98px)');
    mq.addEventListener('change', () => {
        setVar(gridEl, '--col1', '1fr');
        setVar(gridEl, '--col2', '1fr');
        setVar(gridEl, '--col3', '1fr');
    });
}

function attachHandlers(gridEl) {
    const gutters = gridEl.querySelectorAll('.gutter');
    gutters.forEach(g => {
        const idx = parseInt(g.getAttribute('data-gutter'));
        // Use property handler to avoid duplicate listeners
        g.onpointerdown = (e) => onPointerDown(e, gridEl, idx);
    });
}

function onPointerDown(e, gridEl, gutterIndex) {
    if (isMobile()) return;
    e.preventDefault();
    const startX = e.clientX;
    const rect = gridEl.getBoundingClientRect();
    const gW = parseFloat(getVar(gridEl, '--gutter')) || 6;
    const cols = getColsPx(gridEl);
    const numCols = cols.length;
    const gutterCount = gridEl.querySelectorAll('.gutter').length;
    const total = rect.width - gutterCount * gW; // total width for columns only
    const min = 120; // px min per column

    function setFromPixels(px1, px2, px3) {
        if (numCols === 2) {
            const sum = Math.max(1, px1 + px2);
            setVar(gridEl, '--col1', `${px1 / sum}fr`);
            setVar(gridEl, '--col2', `${px2 / sum}fr`);
        } else {
            const sum = Math.max(1, px1 + px2 + px3);
            setVar(gridEl, '--col1', `${px1 / sum}fr`);
            setVar(gridEl, '--col2', `${px2 / sum}fr`);
            setVar(gridEl, '--col3', `${px3 / sum}fr`);
        }
    }

    function onMove(ev) {
        const dx = ev.clientX - startX;
        if (numCols === 2) {
            // Only gutter 1 exists
            const c1 = cols[0];
            const c2 = cols[1];
            let new1 = clamp(c1 + dx, min, total - min);
            let new2 = Math.max(min, total - new1);
            setFromPixels(new1, new2);
            return;
        }

        // 3-column logic
        const c1 = cols[0];
        const c2 = cols[1];
        const c3 = cols[2];
        if (gutterIndex === 1) {
            let new1 = clamp(c1 + dx, min, total - min - min); // leave room for c2 and c3
            let new2 = clamp(c2 - (new1 - c1), min, total - min - new1);
            const remaining = Math.max(min, total - new1 - new2);
            setFromPixels(new1, new2, remaining);
        } else {
            // between col2 and col3
            let new2 = clamp(c2 + dx, min, total - min - c1);
            let new3 = clamp(c3 - (new2 - c2), min, total - min - c1 - new2);
            const c1Fixed = c1;
            const sum = c1Fixed + new2 + new3;
            setVar(gridEl, '--col1', `${c1Fixed / sum}fr`);
            setVar(gridEl, '--col2', `${new2 / sum}fr`);
            setVar(gridEl, '--col3', `${new3 / sum}fr`);
        }
    }

    function onUp() {
        window.removeEventListener('pointermove', onMove);
        window.removeEventListener('pointerup', onUp);
    }

    window.addEventListener('pointermove', onMove, { passive: true });
    window.addEventListener('pointerup', onUp, { passive: true, once: true });
}

function getColsPx(gridEl) {
    const cols = gridEl.querySelectorAll('.col');
    return Array.from(cols).map(c => c.getBoundingClientRect().width);
}

function getVar(el, name) {
    return getComputedStyle(el).getPropertyValue(name).trim();
}

function setVar(el, name, value) {
    el.style.setProperty(name, value);
}

function isMobile() {
    return window.matchMedia('(max-width: 1023.98px)').matches;
}

function clamp(v, min, max) {
    return Math.max(min, Math.min(max, v));
}
