import React, { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { formatMeters, formatSmart } from "./Formatters";
// --- GLOBAL BINDINGS (C# TO UI) ---

const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool");
const toolBoxVisible$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed");

const helixDiameter$ = bindValue<number>("MertsToolBox", "HelixDiameter");
const helixDiameterStepIndex$ = bindValue<number>("MertsToolBox", "HelixDiameterStepIndex");

const helixTurns$ = bindValue<number>("MertsToolBox", "HelixTurns");
const helixTurnStepIndex$ = bindValue<number>("MertsToolBox", "HelixTurnStepIndex");

const helixClearance$ = bindValue<number>("MertsToolBox", "HelixClearance");
const helixClearanceStepIndex$ = bindValue<number>("MertsToolBox", "HelixClearanceStepIndex");

const showSnapRow$ = bindValue<boolean>("MertsToolBox", "ShowSnapRow", false);
const isSnapGeometryActive$ = bindValue<boolean>("MertsToolBox", "IsSnapGeometryActive");
const isSnapNetSideActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetSideActive");
const isSnapNetAreaActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetAreaActive");



// --- COMPONENT DEFINITION ---

export const HelixPanelSection = ({
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

    const rawShow = isToolBoxAllowed && activeTool === "Helix";
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

    const diameter = useValue(helixDiameter$) as number;
    const diameterStepIndex = useValue(helixDiameterStepIndex$) as number;

    const turns = useValue(helixTurns$) as number;
    const turnStepIndex = useValue(helixTurnStepIndex$) as number;

    const clearance = useValue(helixClearance$) as number;
    const clearanceStepIndex = useValue(helixClearanceStepIndex$) as number;

    const showSnapRow = useValue(showSnapRow$) as Boolean;
    const isSnapGeometryActive = useValue(isSnapGeometryActive$) as boolean;
    const isSnapNetSideActive = useValue(isSnapNetSideActive$) as boolean;
    const isSnapNetAreaActive = useValue(isSnapNetAreaActive$) as boolean;    
    
    const {
        itemClass,
        labelClass,
        contentClass,
        buttonClass,
        iconButtonClass,
        startButtonClass,
        endButtonClass,
        numberFieldClass,
        indicatorClass
    } = vanillaClasses;

    // --- RENDER ---

    if (!delayedShow) return null;

    return (
        <div
            className={`helix-panel-container`}
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
            }}>Helix Intersection</div>

            {/* DIAMETER ROW */}
            <div className={itemClass}>
                <div className={labelClass}>Diameter</div>
                <div className={contentClass}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${startButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "HelixDiameterDown");
                        }}
                    >
                        <img className={vanillaClasses.iconClass} src="Media/Glyphs/ThickStrokeArrowDown.svg" alt="Down" />
                    </button>

                    <div className={numberFieldClass}>{formatMeters(diameter)}</div>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${endButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "HelixDiameterUp");
                        }}
                    >
                        <img className={vanillaClasses.iconClass} src="Media/Glyphs/ThickStrokeArrowUp.svg" alt="Up" />
                    </button>

                    <button
                        className={`${buttonClass} ${iconButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "HelixDiameterStep");
                        }}
                        title={`Step Index: ${diameterStepIndex}`}
                    >
                        <svg className={indicatorClass} viewBox="0 0 20 16" >
                            <path d="M0,12h4v4h-4Z" fill={diameterStepIndex >= 0 ? "#1e83aa" : "#424242"}></path>
                            <path d="M5,8h4v8h-4Z" fill={diameterStepIndex >= 1 ? "#1e83aa" : "#424242"}></path>
                            <path d="M10,4h4v12h-4Z" fill={diameterStepIndex >= 2 ? "#1e83aa" : "#424242"}></path>
                            <path d="M15,0h4v16h-4Z" fill={diameterStepIndex >= 3 ? "#1e83aa" : "#424242"}></path>
                        </svg>
                    </button>
                </div>
            </div>

            {/* TURNS ROW */}
            <div className={itemClass}>
                <div className={labelClass}>Turns</div>
                <div className={contentClass}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${startButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "HelixTurnsDown");
                        }}
                    >
                        <img className={vanillaClasses.iconClass} src="Media/Glyphs/ThickStrokeArrowDown.svg" alt="Down" />
                    </button>

                    <div className={numberFieldClass}>{formatSmart(turns)}</div>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${endButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "HelixTurnsUp");
                        }}
                    >
                        <img className={vanillaClasses.iconClass} src="Media/Glyphs/ThickStrokeArrowUp.svg" alt="Up" />
                    </button>

                    <button
                        className={`${buttonClass} ${iconButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "HelixTurnsStep");
                        }}
                        title={`Step Index: ${turnStepIndex}`}
                    >
                        <svg className={indicatorClass} viewBox="0 0 20 16" >
                            <path d="M0,12h4v4h-4Z" fill={turnStepIndex >= 0 ? "#1e83aa" : "#424242"}></path>
                            <path d="M5,8h4v8h-4Z" fill={turnStepIndex >= 1 ? "#1e83aa" : "#424242"}></path>
                            <path d="M10,4h4v12h-4Z" fill={turnStepIndex >= 2 ? "#1e83aa" : "#424242"}></path>
                            <path d="M15,0h4v16h-4Z" fill={turnStepIndex >= 3 ? "#1e83aa" : "#424242"}></path>
                        </svg>
                    </button>
                </div>
            </div>

            {/* CLEARANCE ROW */}
            <div className={itemClass}>
                <div className={labelClass}>Clearance</div>
                <div className={contentClass}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${startButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "HelixClearanceDown");
                        }}
                    >
                        <img className={vanillaClasses.iconClass} src="Media/Glyphs/ThickStrokeArrowDown.svg" alt="Down" />
                    </button>

                    <div className={numberFieldClass}>{formatMeters(clearance)}</div>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${endButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "HelixClearanceUp");
                        }}
                    >
                        <img className={vanillaClasses.iconClass} src="Media/Glyphs/ThickStrokeArrowUp.svg" alt="Up" />
                    </button>

                    <button
                        className={`${buttonClass} ${iconButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "HelixClearanceStep");
                        }}
                        title={`Step Index: ${clearanceStepIndex}`}
                    >
                        <svg className={indicatorClass} viewBox="0 0 20 16" >
                            <path d="M0,12h4v4h-4Z" fill={clearanceStepIndex >= 0 ? "#1e83aa" : "#424242"}></path>
                            <path d="M5,8h4v8h-4Z" fill={clearanceStepIndex >= 1 ? "#1e83aa" : "#424242"}></path>
                            <path d="M10,4h4v12h-4Z" fill={clearanceStepIndex >= 2 ? "#1e83aa" : "#424242"}></path>
                            <path d="M15,0h4v16h-4Z" fill={clearanceStepIndex >= 3 ? "#1e83aa" : "#424242"}></path>
                        </svg>
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
                                trigger("MertsToolBox", "HelixToggleSnap", "Geometry");
                            }}
                        >
                            <img className={vanillaClasses.iconClass} src="Media/Tools/Snap Options/ExistingGeometry.svg" alt="Geometry" />
                        </button>

                        <button
                            className={`${buttonClass} ${iconButtonClass} ${isSnapNetSideActive ? "selected" : ""}`}
                            onMouseDown={(e) => {
                                e.preventDefault();
                                e.stopPropagation();
                                if (e.button !== 0) return;
                                trigger("MertsToolBox", "HelixToggleSnap", "NetSide");
                            }}
                        >
                            <img className={vanillaClasses.iconClass} src="Media/Tools/Snap Options/NetSide.svg" alt="Road Side" />
                        </button>

                        <button
                            className={`${buttonClass} ${iconButtonClass} ${isSnapNetAreaActive ? "selected" : ""}`}
                            onMouseDown={(e) => {
                                e.preventDefault();
                                e.stopPropagation();
                                if (e.button !== 0) return;
                                trigger("MertsToolBox", "HelixToggleSnap", "NetArea");
                            }}
                        >
                            <img className={vanillaClasses.iconClass} src="Media/Tools/Snap Options/NetArea.svg" alt="Road Node" />
                        </button>
                    </div>
                </div>
            )}
        </div>
    );
};