import React, { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import styles from "./ToolBoxPanel.module.scss";
import substractIcon from "./Icons/Subtract.svg";
import { MertSlider } from "./MertSlider";
import {formatMeters, formatSmart } from "./Formatters";
// --- GLOBAL BINDINGS (C# TO UI) ---

const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool");
const toolBoxVisible$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed");

const superEllipseWidth$ = bindValue<number>("MertsToolBox", "SuperEllipseWidth");
const superEllipseWidthStepIndex$ = bindValue<number>("MertsToolBox", "SuperEllipseWidthStepIndex");

const superEllipseLength$ = bindValue<number>("MertsToolBox", "SuperEllipseLength");
const superEllipseLengthStepIndex$ = bindValue<number>("MertsToolBox", "SuperEllipseLengthStepIndex");

const superEllipseN$ = bindValue<number>("MertsToolBox", "SuperEllipseN");

const isSnapGeometryActive$ = bindValue<boolean>("MertsToolBox", "IsSnapGeometryActive");
const isSnapNetSideActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetSideActive");
const isSnapNetAreaActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetAreaActive");

const isSubstractActive$ = bindValue<boolean>("MertsToolBox", "IsSubstractActive");

// --- HELPER FUNCTIONS ---

const uiPing = () => trigger("MertsToolBox", "UiInteracted");

// --- COMPONENT DEFINITION ---

export const SuperEllipsePanelSection = ({
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
    };
}) => {

    // --- VISIBILITY & LIFECYCLE ---

    const activeTool = useValue(activeToolMode$) as string;
    const isToolBoxAllowed = useValue(toolBoxVisible$) as boolean;

    const rawShow = isToolBoxAllowed && activeTool === "SuperEllipse";
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

    const width = useValue(superEllipseWidth$) as number;
    const widthStepIndex = useValue(superEllipseWidthStepIndex$) as number;

    const length = useValue(superEllipseLength$) as number;
    const lengthStepIndex = useValue(superEllipseLengthStepIndex$) as number;

    const nValue = useValue(superEllipseN$) as number;

    const isSnapGeometryActive = useValue(isSnapGeometryActive$) as boolean;
    const isSnapNetSideActive = useValue(isSnapNetSideActive$) as boolean;
    const isSnapNetAreaActive = useValue(isSnapNetAreaActive$) as boolean;

    const isSubstractActive = useValue(isSubstractActive$);

    const {
        buttonClass,
        iconClass,
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
            className={`superellipse-panel-container ${styles.circlePanel}`}
            onMouseDown={(e) => { e.preventDefault(); e.stopPropagation(); uiPing(); }}
            onContextMenu={(e) => { e.preventDefault(); e.stopPropagation(); }}
        >
            <div className={styles.panelHeader}>Super Ellipse</div>

            {/* WIDTH ROW */}
            <div className={styles.panelRow}>
                <div className={styles.rowLabel}>Width</div>
                <div className={styles.rowContent}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${startButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "SuperEllipseWidthDown");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowDown.svg" alt="Down" />
                    </button>

                    <div className={numberFieldClass}>{formatMeters(width)}</div>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${endButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "SuperEllipseWidthUp");
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
                            uiPing();
                            trigger("MertsToolBox", "SuperEllipseWidthStep");
                        }}
                        title={`Step Index: ${widthStepIndex}`}
                    >
                        <svg className={indicatorClass} viewBox="0 0 20 16" >
                            <path d="M0,12h4v4h-4Z" fill={widthStepIndex >= 0 ? "#1e83aa" : "#424242"}></path>
                            <path d="M5,8h4v8h-4Z" fill={widthStepIndex >= 1 ? "#1e83aa" : "#424242"}></path>
                            <path d="M10,4h4v12h-4Z" fill={widthStepIndex >= 2 ? "#1e83aa" : "#424242"}></path>
                            <path d="M15,0h4v16h-4Z" fill={widthStepIndex >= 3 ? "#1e83aa" : "#424242"}></path>
                        </svg>
                    </button>
                </div>
            </div>

            {/* LENGTH ROW */}
            <div className={styles.panelRow}>
                <div className={styles.rowLabel}>Length</div>
                <div className={styles.rowContent}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${startButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "SuperEllipseLengthDown");
                        }}
                    >
                        <img className={iconClass} src="Media/Glyphs/ThickStrokeArrowDown.svg" alt="Down" />
                    </button>

                    <div className={numberFieldClass}>{formatMeters(length)}</div>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${endButtonClass}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "SuperEllipseLengthUp");
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
                            uiPing();
                            trigger("MertsToolBox", "SuperEllipseLengthStep");
                        }}
                        title={`Step Index: ${lengthStepIndex}`}
                    >
                        <svg className={indicatorClass} viewBox="0 0 20 16" >
                            <path d="M0,12h4v4h-4Z" fill={lengthStepIndex >= 0 ? "#1e83aa" : "#424242"}></path>
                            <path d="M5,8h4v8h-4Z" fill={lengthStepIndex >= 1 ? "#1e83aa" : "#424242"}></path>
                            <path d="M10,4h4v12h-4Z" fill={lengthStepIndex >= 2 ? "#1e83aa" : "#424242"}></path>
                            <path d="M15,0h4v16h-4Z" fill={lengthStepIndex >= 3 ? "#1e83aa" : "#424242"}></path>
                        </svg>
                    </button>
                </div>
            </div>

            {/* N VALUE (CURVATURE) ROW */}
            <div className={styles.panelRow}>
                <div className={styles.rowLabel}>N Value</div>
                <div
                    className={styles.rowContent}
                    style={{
                        width: "100%",
                        alignItems: "center",
                        display: "flex"
                    }}
                >
                    <MertSlider
                        min={1}
                        max={15}
                        step={0.1}
                        value={nValue}
                        onChange={(newVal) => {
                            uiPing();
                            trigger("MertsToolBox", "SuperEllipseSetN", newVal);
                        }}
                        formatValue={(v) => v.toFixed(1)}
                    />
                </div>
            </div>

            {/* SNAP ROW */}
            <div className={styles.panelRow}>
                <div className={styles.rowLabel}>Snap</div>
                <div className={styles.rowContent}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${isSnapGeometryActive ? "selected" : ""}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "SuperEllipseToggleSnap", "Geometry");
                        }}
                    >
                        <img className={iconClass} src="Media/Tools/Snap Options/ExistingGeometry.svg" alt="Geometry" />
                    </button>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${isSnapNetSideActive ? "selected" : ""}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "SuperEllipseToggleSnap", "NetSide");
                        }}
                    >
                        <img className={iconClass} src="Media/Tools/Snap Options/NetSide.svg" alt="Road Side" />
                    </button>

                    <button
                        className={`${buttonClass} ${iconButtonClass} ${isSnapNetAreaActive ? "selected" : ""}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "SuperEllipseToggleSnap", "NetArea");
                        }}
                    >
                        <img className={iconClass} src="Media/Tools/Snap Options/NetArea.svg" alt="Road Node" />
                    </button>
                </div>
            </div>

            {/* SUBSTRACT ROW */}
            <div className={styles.panelRow}>
                <div className={styles.rowLabel}>Substract (Experimental)</div>
                <div className={styles.rowContent}>
                    <button
                        className={`${buttonClass} ${iconButtonClass} ${isSubstractActive ? "selected" : ""}`}
                        onMouseDown={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            if (e.button !== 0) return;
                            uiPing();
                            trigger("MertsToolBox", "ToggleEllipseSubstract");
                        }}
                    >
                        <img src={substractIcon} className={iconClass} alt="Substract" />
                    </button>
                </div>
            </div>
        </div>
    );
};