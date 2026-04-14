import { ModRegistrar } from "cs2/modding";
import React, { useEffect } from "react";
import { trigger, bindValue, useValue } from "cs2/api";

import { VanillaResolver } from "./VanilliaResolver";

import { CirclePanelSection } from "./CirclePanelSection";
import { HelixPanelSection } from "./HelixPanelSection";
import { SuperEllipsePanelSection } from "./SuperEllipsePanelSection";
import { GridPanelSection } from "./GridPanelSection";
import { ToolBoxActionHints } from "./ToolBoxActionHints";

import circleIcon from "./Icons/Circle.svg";
import helixIcon from "./Icons/Helix.svg";
import superEllipseIcon from "./Icons/Ellipse.svg";
import gridIcon from "./Icons/Grid.svg";

type ToolId = "Circle" | "Helix" | "SuperEllipse" | "Grid";

type ToolDef = {
    id: ToolId;
    icon: string;
    tooltip: string;
};

const TOOL_DEFS: ToolDef[] = [
    { id: "Circle", icon: circleIcon, tooltip: "Perfect Circle" },
    { id: "Helix", icon: helixIcon, tooltip: "Helix Intersection" },
    { id: "SuperEllipse", icon: superEllipseIcon, tooltip: "Super Ellipse" },
    { id: "Grid", icon: gridIcon, tooltip: "Smart Grid" }
];

const PRELOAD_ICON_SRCS = TOOL_DEFS.map((tool) => tool.icon);

const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool", "None");
const isToolBoxAllowed$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed", false);

let hasPreloadedIcons = false;

function preloadAllToolIcons() {
    if (hasPreloadedIcons) return;
    hasPreloadedIcons = true;

    PRELOAD_ICON_SRCS.forEach((src) => {
        const img = new Image();
        img.src = src;
    });
}

const ToolBoxModeRow = () => {
    const activeTool = useValue(activeToolMode$);

    return (
        <VanillaResolver.instance.Section title="Mert's ToolBox">
            {TOOL_DEFS.map((tool) => {
                const isSelected = activeTool === tool.id;

                return (
                    <VanillaResolver.instance.ToolButton
                        key={tool.id}
                        src={tool.icon}
                        selected={isSelected}
                        tooltip={tool.tooltip} 
                        focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                        onSelect={() => trigger("MertsToolBox", "ToggleTool", tool.id)}
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
            const activeTool = useValue(activeToolMode$) as string;
            const isActive = isAllowed && activeTool !== "None";

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
                    <HelixPanelSection  />
                    <SuperEllipsePanelSection />
                    <GridPanelSection   />
                </div>
            );
        };
    });
};

export default register;