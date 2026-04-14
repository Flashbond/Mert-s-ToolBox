import React, { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { formatMeters, formatSmart } from "./Formatters";
// DİKKAT: Resolver import ediliyor!
import { VanillaResolver } from "./VanilliaResolver";

// --- GLOBAL BINDINGS (C# TO UI) ---
const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool");
const toolBoxVisible$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed");

const helixDiameter$ = bindValue<number>("MertsToolBox", "HelixDiameter");
const helixDiameterStepIndex$ = bindValue<number>("MertsToolBox", "HelixDiameterStepIndex");

const helixTurns$ = bindValue<number>("MertsToolBox", "HelixTurns");
const helixTurnStepIndex$ = bindValue<number>("MertsToolBox", "HelixTurnStepIndex");

const helixClearance$ = bindValue<number>("MertsToolBox", "HelixClearance");
const helixClearanceStepIndex$ = bindValue<number>("MertsToolBox", "HelixClearanceStepIndex");

const helixDiameterStepSize$ = bindValue<number>("MertsToolBox", "HelixDiameterStepSize");
const helixTurnStepSize$ = bindValue<number>("MertsToolBox", "HelixTurnStepSize");
const helixClearanceStepSize$ = bindValue<number>("MertsToolBox", "HelixClearanceStepSize");

const showSnapRow$ = bindValue<boolean>("MertsToolBox", "ShowSnapRow", false);
const isSnapGeometryActive$ = bindValue<boolean>("MertsToolBox", "IsSnapGeometryActive");
const isSnapNetSideActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetSideActive");
const isSnapNetAreaActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetAreaActive");

// --- COMPONENT DEFINITION ---
export const HelixPanelSection = () => {

    // --- VISIBILITY & LIFECYCLE ---
    const activeTool = useValue(activeToolMode$) as string;
    const isToolBoxAllowed = useValue(toolBoxVisible$) as boolean;

    const rawShow = isToolBoxAllowed && activeTool === "Helix";
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
    const diameter = useValue(helixDiameter$) as number;
    const diameterStepIndex = useValue(helixDiameterStepIndex$) as number;

    const turns = useValue(helixTurns$) as number;
    const turnStepIndex = useValue(helixTurnStepIndex$) as number;

    const clearance = useValue(helixClearance$) as number;
    const clearanceStepIndex = useValue(helixClearanceStepIndex$) as number;

    const diameterStepSize = useValue(helixDiameterStepSize$) as number;
    const turnStepSize = useValue(helixTurnStepSize$) as number;
    const clearanceStepSize = useValue(helixClearanceStepSize$) as number;

    const showSnapRow = useValue(showSnapRow$) as boolean;
    const isSnapGeometryActive = useValue(isSnapGeometryActive$) as boolean;
    const isSnapNetSideActive = useValue(isSnapNetSideActive$) as boolean;
    const isSnapNetAreaActive = useValue(isSnapNetAreaActive$) as boolean;

    // --- RENDER ---
    if (!delayedShow) return null;

    return (
        <div
            className={`helix-panel-container`}
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
            }}>Helix Intersection</div>

            {/* DIAMETER ROW */}
            <VanillaResolver.instance.Section title="Diameter">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixDiameterDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>{formatMeters(diameter)}</div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixDiameterUp")}
                />

                <VanillaResolver.instance.StepToolButton
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    selectedValue={diameterStepIndex}
                    values={[0, 1, 2, 3]}
                    tooltip={`Step ${diameterStepSize}`}
                    onSelect={() => trigger("MertsToolBox", "HelixDiameterStep")}
                />
            </VanillaResolver.instance.Section>

            {/* TURNS ROW */}
            <VanillaResolver.instance.Section title="Turns">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixTurnsDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>{formatSmart(turns)}</div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixTurnsUp")}
                />
                <VanillaResolver.instance.StepToolButton
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    selectedValue={turnStepIndex}
                    values={[0, 1, 2, 3]}
                    tooltip={`Step ${turnStepSize}`}
                    onSelect={() => trigger("MertsToolBox", "HelixTurnsStep")}
                />
            </VanillaResolver.instance.Section>

            {/* CLEARANCE ROW */}
            <VanillaResolver.instance.Section title="Clearance">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixClearanceDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>{formatMeters(clearance)}</div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixClearanceUp")}
                />
                <VanillaResolver.instance.StepToolButton
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    selectedValue={clearanceStepIndex}
                    values={[0, 1, 2, 3]}
                    tooltip={`Step ${clearanceStepSize}`}
                    onSelect={() => trigger("MertsToolBox", "HelixClearanceStep")}
                />
            </VanillaResolver.instance.Section>

            {/* SNAP ROW (CONDITIONAL RENDER) */}
            {showSnapRow && (
                <VanillaResolver.instance.Section title="Snap">
                    <VanillaResolver.instance.ToolButton
                        src="Media/Tools/Snap Options/ExistingGeometry.svg"
                        selected={isSnapGeometryActive}
                        focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                        onSelect={() => trigger("MertsToolBox", "HelixToggleSnap", "Geometry")}
                    />

                    <VanillaResolver.instance.ToolButton
                        src="Media/Tools/Snap Options/NetSide.svg"
                        selected={isSnapNetSideActive}
                        focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                        onSelect={() => trigger("MertsToolBox", "HelixToggleSnap", "NetSide")}
                    />

                    <VanillaResolver.instance.ToolButton
                        src="Media/Tools/Snap Options/NetArea.svg"
                        selected={isSnapNetAreaActive}
                        focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                        onSelect={() => trigger("MertsToolBox", "HelixToggleSnap", "NetArea")}
                    />
                </VanillaResolver.instance.Section>
            )}
        </div>
    );
};