import React, { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { formatMeters, formatSmart } from "./utils/Formatters";
import { VanillaResolver } from "./utils/VanilliaResolver";
import { parseActiveTool, ActiveTool } from "./utils/ActiveTool";

// --- GLOBAL BINDINGS (C# TO UI) ---
const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool", "None|None");
const toolBoxVisible$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed");

const circleDiameter$ = bindValue<number>("MertsToolBox", "CircleDiameter");
const circleDiameterStepValue$ = bindValue<number>("MertsToolBox", "CircleDiameterStepValue");
const circleDiameterStepArray$ = bindValue<number[]>("MertsToolBox", "CircleDiameterStepArray");

const elevationValue$ = bindValue<number>("MertsToolBox", "ElevationValue");
const elevationStepValue$ = bindValue<number>("MertsToolBox", "ElevationStepValue");
const elevationStepArray$ = bindValue<number[]>("MertsToolBox", "ElevationStepArray");

const isSnapGeometryActive$ = bindValue<boolean>("MertsToolBox", "IsSnapGeometryActive");
const isSnapNetSideActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetSideActive");
const isSnapNetAreaActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetAreaActive");

// --- COMPONENT DEFINITION ---
export const CirclePanelSection = () => {

    // --- VISIBILITY & LIFECYCLE ---

    const activeToolRaw = useValue(activeToolMode$) as string;
    const activeTool = parseActiveTool(activeToolRaw);

    const isToolBoxAllowed = useValue(toolBoxVisible$) as boolean;
    const rawShow: boolean = isToolBoxAllowed && activeTool.id === "Circle";
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
    const diameter = useValue(circleDiameter$) as number;
    const diameterStepValue = useValue(circleDiameterStepValue$) as number;
    const diameterStepValues = useValue(circleDiameterStepArray$) as number[];

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
            className={`circle-panel-container`}
            onMouseDown={(e) => { e.stopPropagation(); }}
            onContextMenu={(e) => { e.stopPropagation(); }}
            style={{ display: "flex", flexDirection: "column" }}
        >
          
            <h3 className={'panel-header'} style={{
                paddingLeft: "12rem"
            }}>{activeTool.name}</h3>

            {/* DIAMETER ROW */}
            <VanillaResolver.instance.Section title="Diameter">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "CircleDiameterDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>
                    {formatMeters(diameter)}
                </div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "CircleDiameterUp")}
                />

                <VanillaResolver.instance.StepToolButton
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    tooltip={`${diameterStepValue}`}
                    values={diameterStepValues}
                    selectedValue={diameterStepValue}
                    onSelect={(val) => {
                        console.log("Gelen Val:", val),
                        trigger("MertsToolBox", "CircleDiameterStep", val);
                    }}
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
                    onSelect={() => trigger("MertsToolBox", "ToggleCircleSnap", "Geometry")}
                    tooltip={`Existing Geometry`}
                />

                <VanillaResolver.instance.ToolButton
                    src="Media/Tools/Snap Options/NetSide.svg"
                    selected={isSnapNetSideActive}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ToggleCircleSnap", "NetSide")}
                    tooltip={`Net Side`}
                />

                <VanillaResolver.instance.ToolButton
                    src="Media/Tools/Snap Options/NetArea.svg"
                    selected={isSnapNetAreaActive}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ToggleCircleSnap", "NetArea")}
                    tooltip={`Net Area`}
                />
            </VanillaResolver.instance.Section>
        </div>
    );
};