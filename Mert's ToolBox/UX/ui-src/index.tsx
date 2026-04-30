import { ModRegistrar } from "cs2/modding";
import React, { useEffect } from "react";
import { trigger, bindValue, useValue } from "cs2/api";

import { VanillaResolver } from "./utils/VanilliaResolver";
import { parseActiveTool, ActiveTool } from "./utils/ActiveTool";

import { CirclePanelSection } from "./CirclePanelSection";
import { HelixPanelSection } from "./HelixPanelSection";
import { SuperEllipsePanelSection } from "./SuperEllipsePanelSection";
import { GridPanelSection } from "./GridPanelSection";
import { ToolBoxActionHints } from "./utils/ToolBoxActionHints";


import circleIcon from "./Icons/Circle.svg";
import helixIcon from "./Icons/Helix.svg";
import superEllipseIcon from "./Icons/Ellipse.svg";
import gridIcon from "./Icons/SmartGrid.svg";

type RawToolDef = {
    id: string;
    name: string;
    icon: string;
};

type ToolDef = {
    id: string;
    icon: string;
    tooltip: string;
};

const ModId = "MertsToolBox";

const toolList$ = bindValue<string>(ModId, "ToolList", "");
const activeToolMode$ = bindValue<string>(ModId, "ActiveTool", "None");
const isToolBoxAllowed$ = bindValue<boolean>(ModId, "IsToolBoxAllowed", false);

const icons: Record<string, string> = {
    Circle: circleIcon,
    Helix: helixIcon,
    Ellipse: superEllipseIcon,
    Grid: gridIcon
};

let hasPreloadedIcons = false;

function buildToolDefs(toolListRaw: string): ToolDef[] {
    if (!toolListRaw) return [];

    return toolListRaw
        .split(";")
        .map((entry: string) => {
            const [id, name, icon] = entry.split("|");

            return {
                id: id || "",
                icon: icons[icon || id] ?? "",
                tooltip: name || id || ""
            };
        })
        .filter((tool: ToolDef) => tool.id !== "" && tool.icon !== "");
}

function preloadAllToolIcons() {
    if (hasPreloadedIcons) return;
    hasPreloadedIcons = true;

    Object.values(icons).forEach((src: string) => {
        const img = new Image();
        img.src = src;
    });
}

const ToolBoxModeRow = () => {
    const activeTool = useValue(activeToolMode$) as string;
    const toolsJson = useValue(toolList$) as string;
    const toolDefs = buildToolDefs(toolsJson);

    return (
        <VanillaResolver.instance.Section title="Mert's ToolBox">
            {toolDefs.map((tool: ToolDef) => {
                const isSelected = activeTool === tool.id;

                return (
                    <VanillaResolver.instance.ToolButton
                        key={tool.id}
                        src={tool.icon}
                        selected={isSelected}
                        tooltip={tool.tooltip}
                        focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                        onSelect={() => trigger(ModId, "ToggleTool", tool.id)}
                    />
                );
            })}
        </VanillaResolver.instance.Section>
    );
};

const register: ModRegistrar = (moduleRegistry) => {
    VanillaResolver.setRegistry(moduleRegistry);

    const mouseToolPath = "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx";

    moduleRegistry.extend(mouseToolPath, "MouseToolOptions", (OriginalMouseToolOptions: any) => {
        return (props: any) => {
            const isAllowed = useValue(isToolBoxAllowed$) as boolean;
            const activeToolJson = useValue(activeToolMode$) as string;
            const activeTool = parseActiveTool(activeToolJson);
            const isActive = isAllowed && activeTool.id !== "None";

            useEffect(() => {
                preloadAllToolIcons();
            }, []);

            if (!isActive) {
                return (
                    <>
                        <OriginalMouseToolOptions {...props} />
                        {isAllowed && <ToolBoxModeRow />}
                    </>
                );
            }

            return (
                <div
                    className="merts-toolbox-root"
                    style={{
                        width: "100%",
                        display: "flex",
                        flexDirection: "column",
                        pointerEvents: "auto"
                    }}
                >
                    <ToolBoxActionHints />

                    <CirclePanelSection />
                    <HelixPanelSection />
                    <SuperEllipsePanelSection />
                    <GridPanelSection />
                </div>
            );
        };
    });
};

export default register;