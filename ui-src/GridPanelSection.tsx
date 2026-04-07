import React, { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import alternatingIcon from "./Icons/Alternating.svg";
import orientationIcon from "./Icons/Orientation.svg";
import { formatUnits, formatSmart } from "./Formatters";
// --- GLOBAL BINDINGS (C# TO UI) ---

const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool");
const toolBoxVisible$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed");

const gridBlockWidth$ = bindValue<number>("MertsToolBox", "GridBlockWidth");
const gridBlockLength$ = bindValue<number>("MertsToolBox", "GridBlockLength");
const gridColumns$ = bindValue<number>("MertsToolBox", "GridColumns");
const gridRows$ = bindValue<number>("MertsToolBox", "GridRows");

const gridAlternating$ = bindValue<boolean>("MertsToolBox", "GridAlternating");
const gridOrientationLeftBottom$ = bindValue<boolean>("MertsToolBox", "GridOrientationLeftBottom");

const showSnapRow$ = bindValue<boolean>("MertsToolBox", "ShowSnapRow", false);
const isSnapGeometryActive$ = bindValue<boolean>("MertsToolBox", "IsSnapGeometryActive");
const isSnapNetSideActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetSideActive");
const isSnapNetAreaActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetAreaActive");

const gridIsOneWaySupported$ = bindValue<boolean>("MertsToolBox", "GridIsOneWaySupported");

// --- COMPONENT DEFINITION ---

export const GridPanelSection = ({
    vanillaClasses
}: {
        vanillaClasses: {
        itemClass: string;
        labelClass: string;
        contentClass: string;
        buttonClass: string;
        iconClass: string;
        iconButtonClass: string;
        startButtonClass: string;
        endButtonClass: string;
        numberFieldClass: string;
        indicatorClass: string;
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

    const showSnapRow = useValue(showSnapRow$) as Boolean;
    const isSnapGeometryActive = useValue(isSnapGeometryActive$) as boolean;
    const isSnapNetSideActive = useValue(isSnapNetSideActive$) as boolean;
    const isSnapNetAreaActive = useValue(isSnapNetAreaActive$) as boolean;
    
    const {
        itemClass,
        labelClass,
        contentClass,
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
            className={`grid-panel-container`}
            onMouseDown={(e) => { e.preventDefault(); e.stopPropagation(); }}
            onContextMenu={(e) => { e.preventDefault(); e.stopPropagation(); }}
        >
            <div className={'panel-header'} style={{
                fontSize: "16rem",
                fontWeight: 700,
                paddingTop: "4rem",
                paddingLeft: "12rem",
                paddingRight: "12rem",
                paddingBottom: "4rem"
            }}>Smart Grid</div>

            {/* BLOCK WIDTH ROW */}
            <div className={itemClass}>
                <div className={labelClass}>Block Width</div>
                <div className={contentClass}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${startButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
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
                            trigger("MertsToolBox", "GridBlockWidthUp");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowUp.svg" alt="Up" />
                    </button>
                </div>
            </div>

            {/* BLOCK DEPTH ROW */}
            <div className={itemClass}>
                <div className={labelClass}>Block Depth</div>
                <div className={contentClass}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${startButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
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
                            trigger("MertsToolBox", "GridBlockLengthUp");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowUp.svg" alt="Up" />
                    </button>
                </div>
            </div>

            {/* COLUMNS ROW */}
            <div className={itemClass}>
                <div className={labelClass}>Columns</div>
                <div className={contentClass}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${startButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "GridColumnsDown");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowDown.svg" alt="Down" />
                    </button>

                    <div className={numberFieldClass}>{formatSmart(columns)}</div>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${endButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "GridColumnsUp");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowUp.svg" alt="Up" />
                    </button>
                </div>
            </div>

            {/* ROWS ROW */}
            <div className={itemClass}>
                <div className={labelClass}>Rows</div>
                <div className={contentClass}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${startButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "GridRowsDown");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowDown.svg" alt="Down" />
                    </button>

                    <div className={numberFieldClass}>{formatSmart(rows)}</div>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${endButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "GridRowsUp");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowUp.svg" alt="Up" />
                    </button>
                </div>
            </div>

            {/* ONE-WAY PATTERN ROW */}
            <div className={itemClass}>
                <div className={labelClass}>Pattern (One-Way Roads)</div>
                <div className={contentClass}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${isAlternating ? "selected" : ""}`}
                        disabled={!isOneWaySupported}
                        style={{ opacity: isOneWaySupported ? 1 : 0.3, cursor: isOneWaySupported ? 'pointer' : 'not-allowed' }}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0 || !isOneWaySupported) return;
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
                            trigger("MertsToolBox", "GridToggleOrientation");
                        }}
                        title={isOneWaySupported ? "Orientation" : "Requires a one-way road"}
                    >
                        <img className={iconClass} src={orientationIcon} alt="Orientation" draggable={false} />
                    </button>
                </div>
            </div>
            {showSnapRow && (
                <div className={itemClass}>
                <div className={labelClass}>Snap</div>
                    <div className={contentClass}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${isSnapGeometryActive ? "selected" : ""}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "GridToggleSnap", "Geometry");
                        }}
                    >
                        <img src="Media/Tools/Snap Options/ExistingGeometry.svg" className={iconClass} alt="Geometry" />
                    </button>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${isSnapNetSideActive ? "selected" : ""}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "GridToggleSnap", "NetSide");
                        }}
                    >
                        <img src="Media/Tools/Snap Options/NetSide.svg" className={iconClass} alt="Road Side" />
                    </button>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${isSnapNetAreaActive ? "selected" : ""}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "GridToggleSnap", "NetArea");
                        }}
                    >
                        <img src="Media/Tools/Snap Options/NetArea.svg" className={iconClass} alt="Road Node" />
                    </button>
                </div>
            </div>
            )}
        </div>
    );
};