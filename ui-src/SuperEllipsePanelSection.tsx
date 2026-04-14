import React, { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { MertSlider } from "./MertSlider";
import { formatMeters, formatSmart } from "./Formatters";
import { VanillaResolver } from "./VanilliaResolver";

// --- GLOBAL BINDINGS (C# TO UI) ---
const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool");
const toolBoxVisible$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed");

const superEllipseWidth$ = bindValue<number>("MertsToolBox", "SuperEllipseWidth");
const superEllipseWidthStepIndex$ = bindValue<number>("MertsToolBox", "SuperEllipseWidthStepIndex");
const superEllipseWidthStepSize$ = bindValue<number>("MertsToolBox", "SuperEllipseWidthStepSize");

const superEllipseLength$ = bindValue<number>("MertsToolBox", "SuperEllipseLength");
const superEllipseLengthStepIndex$ = bindValue<number>("MertsToolBox", "SuperEllipseLengthStepIndex");
const superEllipseLengthStepSize$ = bindValue<number>("MertsToolBox", "SuperEllipseLengthStepSize");

const superEllipseN$ = bindValue<number>("MertsToolBox", "SuperEllipseN");

const isSnapGeometryActive$ = bindValue<boolean>("MertsToolBox", "IsSnapGeometryActive");
const isSnapNetSideActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetSideActive");
const isSnapNetAreaActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetAreaActive");

// --- COMPONENT DEFINITION ---
export const SuperEllipsePanelSection = () => {

    // --- VISIBILITY & LIFECYCLE ---
    const activeTool = useValue(activeToolMode$) as string;
    const isToolBoxAllowed = useValue(toolBoxVisible$) as boolean;

    const rawShow = isToolBoxAllowed && activeTool === "SuperEllipse";
    const [delayedShow, setDelayedShow] = useState(false);

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
    const widthStepSize = useValue(superEllipseWidthStepSize$) as number;

    const length = useValue(superEllipseLength$) as number;
    const lengthStepIndex = useValue(superEllipseLengthStepIndex$) as number;
    const lengthStepSize = useValue(superEllipseLengthStepSize$) as number;

    const nValue = useValue(superEllipseN$) as number;

    const isSnapGeometryActive = useValue(isSnapGeometryActive$) as boolean;
    const isSnapNetSideActive = useValue(isSnapNetSideActive$) as boolean;
    const isSnapNetAreaActive = useValue(isSnapNetAreaActive$) as boolean;

    // --- RENDER ---
    if (!delayedShow) return null;

    return (
        <div
            className={`superellipse-panel-container`}
            onMouseDown={(e) => { e.stopPropagation(); }}
            onContextMenu={(e) => { e.stopPropagation(); }}
            style={{ display: "flex", flexDirection: "column" }}
        >
            <div className={'panel-header'} style={{
                fontSize: "16rem",
                fontWeight: 700,
                paddingTop: "4rem",
                paddingLeft: "12rem",
                paddingRight: "12rem",
                paddingBottom: "4rem"
            }}>Super Ellipse</div>

            {/* WIDTH ROW */}
            <VanillaResolver.instance.Section title="Width">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SuperEllipseWidthDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>
                    {formatMeters(width)}
                </div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SuperEllipseWidthUp")}
                />
                <VanillaResolver.instance.StepToolButton
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    selectedValue={widthStepIndex}
                    values={[0, 1, 2, 3]}
                    tooltip={`Step ${widthStepSize}`}
                    onSelect={() => trigger("MertsToolBox", "SuperEllipseWidthStep")}
                />
            </VanillaResolver.instance.Section>

            {/* LENGTH ROW */}
            <VanillaResolver.instance.Section title="Length">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SuperEllipseLengthDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>
                    {formatMeters(length)}
                </div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SuperEllipseLengthUp")}
                />
                <VanillaResolver.instance.StepToolButton
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    selectedValue={lengthStepIndex}
                    values={[0, 1, 2, 3]}
                    tooltip={`Step ${lengthStepSize}`}
                    onSelect={() => trigger("MertsToolBox", "SuperEllipseLengthStep")}
                />
            </VanillaResolver.instance.Section>

            {/* N VALUE (CURVATURE) ROW */}
            <VanillaResolver.instance.Section title="N Value">
                <div className={VanillaResolver.instance.mouseToolOptionsTheme.content}>
                    <MertSlider
                        min={1}
                        max={15}
                        step={0.1}
                        value={nValue}
                        onChange={(newVal) => {
                            trigger("MertsToolBox", "SuperEllipseSetN", newVal);
                        }}
                        formatValue={(v) => v.toFixed(1)}
                    />
                </div>
            </VanillaResolver.instance.Section>

            {/* SNAP ROW */}
            <VanillaResolver.instance.Section title="Snap">
                <VanillaResolver.instance.ToolButton
                    src="Media/Tools/Snap Options/ExistingGeometry.svg"
                    selected={isSnapGeometryActive}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SuperEllipseToggleSnap", "Geometry")}
                />

                <VanillaResolver.instance.ToolButton
                    src="Media/Tools/Snap Options/NetSide.svg"
                    selected={isSnapNetSideActive}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SuperEllipseToggleSnap", "NetSide")}
                />

                <VanillaResolver.instance.ToolButton
                    src="Media/Tools/Snap Options/NetArea.svg"
                    selected={isSnapNetAreaActive}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SuperEllipseToggleSnap", "NetArea")}
                />
            </VanillaResolver.instance.Section>
        </div>
    );
};