import { ModRegistrar } from "cs2/modding";
import React, { useEffect, useLayoutEffect, useRef } from "react";
import { trigger, bindValue, useValue } from "cs2/api";

import { VanillaResolver } from "./utils/VanilliaResolver";
import { parseActiveTool, ActiveTool } from "./utils/ActiveTool";

import { ShapePanelSection } from "./ShapePanelSection";
import { HelixPanelSection } from "./HelixPanelSection";
import { SoftBlockPanelSection } from "./SoftBlockPanelSection";
import { GridPanelSection } from "./GridPanelSection";
import { ToolBoxActionHints } from "./utils/ToolBoxActionHints";

import shapeIcon from "./icons/Circle.svg";
import helixIcon from "./icons/Helix.svg";
import softBlockIcon from "./icons/SoftBlock.svg";
import gridIcon from "./icons/SmartGrid.svg";

type ToolDef = {
    id: string;
    icon: string;
    tooltip: string;
};

const ModId = "MertsToolBox";

const toolList$ = bindValue<string>(ModId, "ToolList", "");
const activeToolMode$ = bindValue<string>(ModId, "ActiveTool", "None|None");
const isToolBoxAllowed$ = bindValue<boolean>(ModId, "IsToolBoxAllowed", false);

const icons: Record<string, string> = {
    Shape: shapeIcon,
    Helix: helixIcon,
    SoftBlock: softBlockIcon,
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
    const activeToolRaw = useValue(activeToolMode$) as string;
    const activeTool = parseActiveTool(activeToolRaw);

    const toolsJson = useValue(toolList$) as string;
    const toolDefs = buildToolDefs(toolsJson);

    return (
        <VanillaResolver.instance.Section title="Mert's ToolBox">
            {toolDefs.map((tool: ToolDef) => {
                const isSelected = activeTool.id === tool.id;

                return (
                    <VanillaResolver.instance.ToolButton
                        key={tool.id}
                        src={tool.icon}
                        selected={isSelected}
                        tooltip={tool.tooltip}
                        focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                        onSelect={() => {trigger(ModId, "ToggleTool", tool.id);}}
                    />
                );
            })}
        </VanillaResolver.instance.Section>
    );
};

const KEEP_ROW_MATCHERS: ((row: HTMLElement) => boolean)[] = [
    (row) => /anarchy/i.test(row.textContent ?? ""),
    (row) => Array.from(row.querySelectorAll("img")).some(
        (img) => /anarchy/i.test(img.getAttribute("src") ?? "")),
];

function findRowContainer(host: HTMLElement): HTMLElement | null {
    let el: HTMLElement | null = host;
    while (el && el.children.length === 1)
        el = el.firstElementChild as HTMLElement | null;
    return el;
}

function filterVanillaRows(host: HTMLElement) {
    const container = findRowContainer(host);
    if (!container) return;

    Array.from(container.children).forEach((child) => {
        const row = child as HTMLElement;
        const keep = KEEP_ROW_MATCHERS.some((m) => m(row));
        const display = keep ? "" : "none";
        if (row.style.display !== display)
            row.style.display = display;
    });
}

const register: ModRegistrar = (moduleRegistry) => {
    VanillaResolver.setRegistry(moduleRegistry);

    const mouseToolPath = "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx";

    moduleRegistry.extend(mouseToolPath, "MouseToolOptions", (OriginalMouseToolOptions: any) => {
        return (props: any) => {
            const isAllowed = useValue(isToolBoxAllowed$) as boolean;
            const activeTool = parseActiveTool(useValue(activeToolMode$) as string);
            const isActive = isAllowed && activeTool.id !== "None";
            const hostRef = useRef<HTMLDivElement>(null);

            useEffect(() => { preloadAllToolIcons(); }, []);

            // Vanilla/diğer mod satırlarını gizle, sadece izin verilenleri bırak.
            useLayoutEffect(() => {
                const host = hostRef.current;
                if (!isActive || !host) return;

                filterVanillaRows(host);

                // İçerideki bileşenler yeniden çizildikçe (yeni satır eklenince) filtreyi tekrar uygula.
                if (typeof MutationObserver !== "undefined") {
                    const observer = new MutationObserver(() => filterVanillaRows(host));
                    observer.observe(host, { childList: true, subtree: true });
                    return () => observer.disconnect();
                }

                let frame = 0;
                const tick = () => { filterVanillaRows(host); frame = requestAnimationFrame(tick); };
                frame = requestAnimationFrame(tick);
                return () => cancelAnimationFrame(frame);
            }, [isActive, activeTool.id]);

            if (!isActive) {
                return (
                    <>
                        <OriginalMouseToolOptions {...props} />
                        {isAllowed && <ToolBoxModeRow />}
                    </>
                );
            }

            return (
                <>
                    <div ref={hostRef} className="merts-vanilla-host">
                        <OriginalMouseToolOptions {...props} />
                    </div>

                    <div
                        className="merts-toolbox-root"
                        style={{ width: "100%", display: "flex", flexDirection: "column", pointerEvents: "auto" }}
                    >
                        <ToolBoxActionHints />
                        <ShapePanelSection />
                        <HelixPanelSection />
                        <SoftBlockPanelSection />
                        <GridPanelSection />
                    </div>
                </>
            );
        };
    });
};

export default register;