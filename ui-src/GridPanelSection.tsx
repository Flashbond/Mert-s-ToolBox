import React, { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import styles from "./ToolBoxPanel.module.scss";
import alternatingIcon from "./Icons/Alternating.svg";
import orientationIcon from "./Icons/Orientation.svg";

// --- GLOBAL BINDINGS (C# TO UI) ---

const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool");
const toolBoxVisible$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed");

const gridBlockWidth$ = bindValue<number>("MertsToolBox", "GridBlockWidth");
const gridBlockLength$ = bindValue<number>("MertsToolBox", "GridBlockLength");
const gridColumns$ = bindValue<number>("MertsToolBox", "GridColumns");
const gridRows$ = bindValue<number>("MertsToolBox", "GridRows");

const gridAlternating$ = bindValue<boolean>("MertsToolBox", "GridAlternating");
const gridOrientationLeftBottom$ = bindValue<boolean>("MertsToolBox", "GridOrientationLeftBottom");

const isSnapGeometryActive$ = bindValue<boolean>("MertsToolBox", "IsSnapGeometryActive");
const isSnapNetSideActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetSideActive");
const isSnapNetAreaActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetAreaActive");

const gridIsOneWaySupported$ = bindValue<boolean>("MertsToolBox", "GridIsOneWaySupported");

// --- HELPER FUNCTIONS ---

const uiPing = () => trigger("MertsToolBox", "UiInteracted");

function formatUnits(value: number): string {
    return `${value} U`;
}

function formatCount(value: number): string {
    return `${value}`;
}

// --- COMPONENT DEFINITION ---

export const GridPanelSection = ({
    vanillaClasses
}: {
    vanillaClasses: {
        buttonClass: string;
        iconClass: string;
        iconButtonClass: string;
        startButtonClass: string;
        endButtonClass: string;
        numberFieldClass: string;
        indicatorClass: string;
        isReady: boolean;
    };
}) => {

    // --- VISIBILITY & LIFECYCLE ---

    const activeTool = useValue(activeToolMode$) as string;
    const isToolBoxAllowed = useValue(toolBoxVisible$) as boolean;

    const rawShow = isToolBoxAllowed && activeTool === "Grid";
    const [delayedShow, setDelayedShow] = useState(false);

    // Delays component unmounting by 150ms to allow CSS closing animations to finish
    useEffect(() => {
        let timeoutId: ReturnType<typeof setTimeout> | undefined;

        if (rawShow) {
            setDelayedShow(true);
        } else {
            timeoutId = setTimeout(() => {
                setDelayedShow(false);
            }, 150);
        }

        return () => {
            if (timeoutId) clearTimeout(timeoutId);
        };
    }, [rawShow]);

    // --- DATA BINDING EVALUATION ---

    const blockWidth = useValue(gridBlockWidth$) as number;
    const blockLength = useValue(gridBlockLength$) as number;
    const columns = useValue(gridColumns$) as number;
    const rows = useValue(gridRows$) as number;

    const isAlternating = useValue(gridAlternating$) as boolean;
    const isOrientationLeftBottom = useValue(gridOrientationLeftBottom$) as boolean;
    const isOneWaySupported = useValue(gridIsOneWaySupported$) as boolean;

    // Note: Snap bindings are initialized here in case you want to render a Snap row later.
    const isSnapGeometryActive = useValue(isSnapGeometryActive$) as boolean;
    const isSnapNetSideActive = useValue(isSnapNetSideActive$) as boolean;
    const isSnapNetAreaActive = useValue(isSnapNetAreaActive$) as boolean;

    const {
        buttonClass,
        iconClass,
        iconButtonClass,
        startButtonClass,
        endButtonClass,
        numberFieldClass
    } = vanillaClasses;

    // --- RENDER ---

    if (!delayedShow) return null;

    return (
        <div
            className={`grid-panel-container ${styles.circlePanel}`}
            onMouseDown={(e) => { e.preventDefault(); e.stopPropagation(); uiPing(); }}
            onContextMenu={(e) => { e.preventDefault(); e.stopPropagation(); }}
        >
            <div className={styles.panelHeader}>Smart Grid</div>

            {/* BLOCK WIDTH ROW */}
            <div className={styles.panelRow}>
                <div className={styles.rowLabel}>Block Width</div>
                <div className={styles.rowContent}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${startButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "GridBlockWidthDown");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowDown.svg" alt="Down" />
                    </button>

                    <div className={numberFieldClass}>{formatUnits(blockWidth)}</div>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${endButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "GridBlockWidthUp");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowUp.svg" alt="Up" />
                    </button>
                </div>
            </div>

            {/* BLOCK DEPTH ROW */}
            <div className={styles.panelRow}>
                <div className={styles.rowLabel}>Block Depth</div>
                <div className={styles.rowContent}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${startButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "GridBlockLengthDown");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowDown.svg" alt="Down" />
                    </button>

                    <div className={numberFieldClass}>{formatUnits(blockLength)}</div>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${endButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "GridBlockLengthUp");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowUp.svg" alt="Up" />
                    </button>
                </div>
            </div>

            {/* COLUMNS ROW */}
            <div className={styles.panelRow}>
                <div className={styles.rowLabel}>Columns</div>
                <div className={styles.rowContent}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${startButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "GridColumnsDown");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowDown.svg" alt="Down" />
                    </button>

                    <div className={numberFieldClass}>{formatCount(columns)}</div>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${endButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "GridColumnsUp");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowUp.svg" alt="Up" />
                    </button>
                </div>
            </div>

            {/* ROWS ROW */}
            <div className={styles.panelRow}>
                <div className={styles.rowLabel}>Rows</div>
                <div className={styles.rowContent}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${startButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "GridRowsDown");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowDown.svg" alt="Down" />
                    </button>

                    <div className={numberFieldClass}>{formatCount(rows)}</div>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${endButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "GridRowsUp");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowUp.svg" alt="Up" />
                    </button>
                </div>
            </div>

            {/* ONE-WAY PATTERN ROW */}
            <div className={styles.panelRow}>
                <div className={styles.rowLabel}>Pattern (One-Way Roads)</div>
                <div className={styles.rowContent}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${isAlternating ? "selected" : ""}`}
                        disabled={!isOneWaySupported}
                        style={{ opacity: isOneWaySupported ? 1 : 0.3, cursor: isOneWaySupported ? 'pointer' : 'not-allowed' }}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0 || !isOneWaySupported) return;
                            uiPing();
                            trigger("MertsToolBox", "GridToggleAlternating");
                        }}
                        title={isOneWaySupported ? "Alternating" : "Requires a one-way road"}
                    >
                        <img className={iconClass} src={alternatingIcon} alt="Alternating" draggable={false} />
                    </button>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${isOrientationLeftBottom ? "selected" : ""}`}
                        disabled={!isOneWaySupported}
                        style={{ opacity: isOneWaySupported ? 1 : 0.3, cursor: isOneWaySupported ? 'pointer' : 'not-allowed' }}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0 || !isOneWaySupported) return;
                            uiPing();
                            trigger("MertsToolBox", "GridToggleOrientation");
                        }}
                        title={isOneWaySupported ? "Orientation" : "Requires a one-way road"}
                    >
                        <img className={iconClass} src={orientationIcon} alt="Orientation" draggable={false} />
                    </button>
                </div>
            </div>
        </div>
    );
};