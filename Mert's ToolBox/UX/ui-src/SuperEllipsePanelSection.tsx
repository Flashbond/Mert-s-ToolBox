import React, { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { MertSlider } from "./utils/MertSlider";
import { formatMeters, formatSmart } from "./utils/Formatters";
import { VanillaResolver } from "./utils/VanilliaResolver";
import { parseActiveTool, ActiveTool } from "./utils/ActiveTool";

// --- GLOBAL BINDINGS (C# TO UI) ---
const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool", "None|None");
const toolBoxVisible$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed");

const superEllipseWidth$ = bindValue<number>("MertsToolBox", "SuperEllipseWidth");
const superEllipseWidthStepValue$ = bindValue<number>("MertsToolBox", "SuperEllipseWidthStepValue");
const superEllipseWidthStepArray$ = bindValue<number[]>("MertsToolBox", "SuperEllipseWidthStepArray");

const superEllipseLength$ = bindValue<number>("MertsToolBox", "SuperEllipseLength");
const superEllipseLengthStepValue$ = bindValue<number>("MertsToolBox", "SuperEllipseLengthStepValue");
const superEllipseLengthStepArray$ = bindValue<number[]>("MertsToolBox", "SuperEllipseLengthStepArray");

const superEllipseN$ = bindValue<number>("MertsToolBox", "SuperEllipseN");

const elevationValue$ = bindValue<number>("MertsToolBox", "ElevationValue");
const elevationStepValue$ = bindValue<number>("MertsToolBox", "ElevationStepValue");
const elevationStepArray$ = bindValue<number[]>("MertsToolBox", "ElevationStepArray");

const isSnapGeometryActive$ = bindValue<boolean>("MertsToolBox", "IsSnapGeometryActive");
const isSnapNetSideActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetSideActive");
const isSnapNetAreaActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetAreaActive");

// --- COMPONENT DEFINITION ---
export const SuperEllipsePanelSection = () => {

    // --- VISIBILITY & LIFECYCLE ---
    const activeToolRaw = useValue(activeToolMode$) as string;
    const activeTool = parseActiveTool(activeToolRaw);

    const isToolBoxAllowed = useValue(toolBoxVisible$) as boolean;
    const rawShow: boolean = isToolBoxAllowed && activeTool.id === "Ellipse";
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
    const widthStepValue = useValue(superEllipseWidthStepValue$) as number;
    const widthStepValues = useValue(superEllipseWidthStepArray$) as number[];

    const length = useValue(superEllipseLength$) as number;
    const lengthStepValue = useValue(superEllipseLengthStepValue$) as number;
    const lengthStepValues = useValue(superEllipseLengthStepArray$) as number[];

    const nValue = useValue(superEllipseN$) as number;

    const elevationValue = useValue(elevationValue$) as number;
    const elevationStepValue = useValue(elevationStepValue$) as number;
    const elevationStepValues = useValue(elevationStepArray$) as number[];

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
            <h3 className={'panel-header'} style={{
                paddingLeft: "12rem"
            }}>{activeTool.name}</h3>

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
                    selectedValue={widthStepValue}
                    values={widthStepValues}
                    tooltip={`${widthStepValue}`}
                    onSelect={(val) => {
                        trigger("MertsToolBox", "SuperEllipseWidthStep", val);
                    }}
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
                    selectedValue={lengthStepValue}
                    values={lengthStepValues}
                    tooltip={`${lengthStepValue}`}
                    onSelect={(val) => {
                        trigger("MertsToolBox", "SuperEllipseLengthStep", val);
                    }}
                />
            </VanillaResolver.instance.Section>

            {/* N VALUE (CURVATURE) ROW */}
            <VanillaResolver.instance.Section title="N Value">
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
            </VanillaResolver.instance.Section>

            {/* ELEVATION ROW */}
            <VanillaResolver.instance.Section title="Elevation">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ElevationDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>
                    {formatMeters(elevationValue)}
                </div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ElevationUp")}
                />

                <VanillaResolver.instance.StepToolButton
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    tooltip={`${elevationStepValue}`}
                    values={elevationStepValues}
                    selectedValue={elevationStepValue}
                    onSelect={(val) => {
                        trigger("MertsToolBox", "ElevationStep", val);
                    }}
                />
            </VanillaResolver.instance.Section>

            {/* SNAP ROW */}
            <VanillaResolver.instance.Section title="Snap">
                <VanillaResolver.instance.ToolButton
                    src="Media/Tools/Snap Options/ExistingGeometry.svg"
                    selected={isSnapGeometryActive}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SuperEllipseToggleSnap", "Geometry")}
                    tooltip={`Existing Geometry`}
                />

                <VanillaResolver.instance.ToolButton
                    src="Media/Tools/Snap Options/NetSide.svg"
                    selected={isSnapNetSideActive}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SuperEllipseToggleSnap", "NetSide")}
                    tooltip={`Net Side`}
                />

                <VanillaResolver.instance.ToolButton
                    src="Media/Tools/Snap Options/NetArea.svg"
                    selected={isSnapNetAreaActive}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SuperEllipseToggleSnap", "NetArea")}
                    tooltip={`Net Area`}
                />
            </VanillaResolver.instance.Section>
        </div>
    );
};