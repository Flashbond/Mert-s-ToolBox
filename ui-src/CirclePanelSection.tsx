import React, { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { formatMeters, formatSmart } from "./Formatters";
// --- GLOBAL BINDINGS (C# TO UI) ---

const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool");
const toolBoxVisible$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed");

const circleDiameter$ = bindValue<number>("MertsToolBox", "CircleDiameter");
const circleDiameterStepIndex$ = bindValue<number>("MertsToolBox", "CircleDiameterStepIndex");
const circleDiameterStepSize$ = bindValue<number>("MertsToolBox", "CircleDiameterStepSize");

const isSnapGeometryActive$ = bindValue<boolean>("MertsToolBox", "IsSnapGeometryActive");
const isSnapNetSideActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetSideActive");
const isSnapNetAreaActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetAreaActive");

// --- COMPONENT DEFINITION ---

export const CirclePanelSection = ({
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

    const activeTool = useValue(activeToolMode$);
    const isToolBoxAllowed = useValue(toolBoxVisible$);
    const rawShow = isToolBoxAllowed && activeTool === "Circle";
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

    const diameter = useValue(circleDiameter$);
    const diameterStepIndex = useValue(circleDiameterStepIndex$) ?? 3;
    const diameterStepSize = useValue(circleDiameterStepSize$) ?? 8;

    const isSnapGeometryActive = useValue(isSnapGeometryActive$);
    const isSnapNetSideActive = useValue(isSnapNetSideActive$);
    const isSnapNetAreaActive = useValue(isSnapNetAreaActive$);

    const {
        itemClass,
        labelClass,
        contentClass,
        buttonClass,
        iconButtonClass,
        iconClass,
        startButtonClass,
        endButtonClass,
        numberFieldClass,
        indicatorClass
    } = vanillaClasses;

    // --- RENDER ---

    if (!delayedShow) return null;

    return (
        <div
            className={`circle-panel-container`}
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
            }}>Perfect Circle</div>

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
                            trigger("MertsToolBox", "CircleDiameterDown");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowDown.svg" alt="Down" />
                    </button>

                    <div className={numberFieldClass}>
                        {formatMeters(diameter)}
                    </div>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${endButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "CircleDiameterUp");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowUp.svg" alt="Up" />
                    </button>

                    <button
                        className={`${buttonClass} ${iconButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "CircleDiameterStep");
                        }}
                        title={`Step ${diameterStepSize}`}
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

            {/* SNAP ROW */}
            <div className={itemClass}>
                <div className={labelClass}>Snap</div>
                <div className={contentClass}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${isSnapGeometryActive ? "selected" : ""}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "ToggleCircleSnap", "Geometry");
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
                            trigger("MertsToolBox", "ToggleCircleSnap", "NetSide");
                        }}
                    >
                        <img src="Media/Tools/Snap Options/NetSide.svg" className={iconClass} alt="Side" />
                    </button>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${isSnapNetAreaActive ? "selected" : ""}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            trigger("MertsToolBox", "ToggleCircleSnap", "NetArea");
                        }}
                    >
                        <img src="Media/Tools/Snap Options/NetArea.svg" className={iconClass} alt="Area" />
                    </button>
                </div>
            </div>
        </div>
    );
};