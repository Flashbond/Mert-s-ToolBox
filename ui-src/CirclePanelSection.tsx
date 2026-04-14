import React, { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { formatMeters, formatSmart } from "./Formatters";
import { VanillaResolver } from "./VanilliaResolver";

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
export const CirclePanelSection = () => {

    // --- VISIBILITY & LIFECYCLE ---
    const activeTool = useValue(activeToolMode$);
    const isToolBoxAllowed = useValue(toolBoxVisible$);
    const rawShow = isToolBoxAllowed && activeTool === "Circle";
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
    const diameter = useValue(circleDiameter$);
    const diameterStepIndex = useValue(circleDiameterStepIndex$) ?? 3;
    const diameterStepSize = useValue(circleDiameterStepSize$) ?? 8;

    const isSnapGeometryActive = useValue(isSnapGeometryActive$);
    const isSnapNetSideActive = useValue(isSnapNetSideActive$);
    const isSnapNetAreaActive = useValue(isSnapNetAreaActive$);

    // --- RENDER ---
    if (!delayedShow) return null;

    return (
        <div
            className={`circle-panel-container`}
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
            }}>Perfect Circle</div>

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
                    selectedValue={diameterStepIndex}
                    values={[0, 1, 2, 3]}
                    tooltip={`Step ${diameterStepSize}`}
                    onSelect={() => trigger("MertsToolBox", "CircleDiameterStep")}
                />
            </VanillaResolver.instance.Section>

            {/* SNAP ROW */}
            <VanillaResolver.instance.Section title="Snap">
                <VanillaResolver.instance.ToolButton
                    src="Media/Tools/Snap Options/ExistingGeometry.svg"
                    selected={isSnapGeometryActive}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ToggleCircleSnap", "Geometry")}
                />

                <VanillaResolver.instance.ToolButton
                    src="Media/Tools/Snap Options/NetSide.svg"
                    selected={isSnapNetSideActive}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ToggleCircleSnap", "NetSide")}
                />

                <VanillaResolver.instance.ToolButton
                    src="Media/Tools/Snap Options/NetArea.svg"
                    selected={isSnapNetAreaActive}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ToggleCircleSnap", "NetArea")}
                />
            </VanillaResolver.instance.Section>
        </div>
    );
};