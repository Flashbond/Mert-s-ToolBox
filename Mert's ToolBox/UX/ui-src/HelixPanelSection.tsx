import React, { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import ccwIcon from "./Icons/CounterCW.svg";
import { formatMeters, formatSmart } from "./utils/Formatters";
import { VanillaResolver } from "./utils/VanilliaResolver";
import { parseActiveTool, ActiveTool } from "./utils/ActiveTool";

// --- GLOBAL BINDINGS (C# TO UI) ---
const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool", "None|None");
const toolBoxVisible$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed");

const helixDiameter$ = bindValue<number>("MertsToolBox", "HelixDiameter");
const helixDiameterStepValue$ = bindValue<number>("MertsToolBox", "HelixDiameterStepValue");
const helixDiameterStepArray$ = bindValue<number[]>("MertsToolBox", "HelixDiameterStepArray");

const helixTurns$ = bindValue<number>("MertsToolBox", "HelixTurns");
const helixTurnStepValue$ = bindValue<number>("MertsToolBox", "HelixTurnStepValue");
const helixTurnStepArray$ = bindValue<number[]>("MertsToolBox", "HelixTurnStepArray");

const helixClearance$ = bindValue<number>("MertsToolBox", "HelixClearance");
const helixClearanceStepValue$ = bindValue<number>("MertsToolBox", "HelixClearanceStepValue");
const helixClearanceStepArray$ = bindValue<number[]>("MertsToolBox", "HelixClearanceStepArray");

const helixIsClockwise$ = bindValue<boolean>("MertsToolBox", "HelixIsClockwise");

// --- COMPONENT DEFINITION ---
export const HelixPanelSection = () => {

    // --- VISIBILITY & LIFECYCLE ---
    const activeToolRaw = useValue(activeToolMode$) as string;
    const activeTool = parseActiveTool(activeToolRaw);

    const isToolBoxAllowed = useValue(toolBoxVisible$) as boolean;
    const rawShow: boolean = isToolBoxAllowed && activeTool.id === "Helix";
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
    const diameterStepValue = useValue(helixDiameterStepValue$) as number;
    const diameterStepValues = useValue(helixDiameterStepArray$) as number[];

    const turns = useValue(helixTurns$) as number;
    const turnStepValue = useValue(helixTurnStepValue$) as number;
    const turnStepValues = useValue(helixTurnStepArray$) as number[];

    const clearance = useValue(helixClearance$) as number;
    const clearanceStepValue = useValue(helixClearanceStepValue$) as number;
    const clearanceStepValues = useValue(helixClearanceStepArray$) as number[];

    const isClockwise = useValue(helixIsClockwise$) as boolean;

    // --- RENDER ---
    if (!delayedShow) return null;

    return (
        <div
            className={`helix-panel-container`}
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
                    selectedValue={diameterStepValue}
                    values={diameterStepValues}
                    tooltip={`${diameterStepValue}`}
                    onSelect={(val) => {
                        trigger("MertsToolBox", "HelixDiameterStep", val);
                    }}
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
                    selectedValue={turnStepValue}
                    values={turnStepValues}
                    tooltip={`${turnStepValue}`}
                    onSelect={(val) => {
                        trigger("MertsToolBox", "HelixTurnStep", val);
                    }}
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
                    selectedValue={clearanceStepValue}
                    values={clearanceStepValues}
                    tooltip={`${clearanceStepValue}`}
                    onSelect={(val) => {
                        trigger("MertsToolBox", "HelixClearanceStep", val);
                    }}
                />
            </VanillaResolver.instance.Section>

            <VanillaResolver.instance.Section title="Direction">
                <VanillaResolver.instance.ToolButton
                    src={ccwIcon}
                    selected={!isClockwise}
                    tooltip={!isClockwise ? "Counter-Clockwise ON" : "Counter-Clockwise OFF"}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixToggleDirection")}
                />
            </VanillaResolver.instance.Section>
        </div>
    );
};