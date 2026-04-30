import { FocusKey, Theme, UniqueFocusKey } from "cs2/bindings";
import { ModuleRegistry } from "cs2/modding";
import { HTMLAttributes } from "react";

type PropsToolButton = {
    focusKey?: UniqueFocusKey | null
    src?: string
    selected?: boolean
    multiSelect?: boolean
    disabled?: boolean
    tooltip?: string | JSX.Element | null
    selectSound?: any
    uiTag?: string
    className?: string
    children?: string | JSX.Element | JSX.Element[]
    onSelect?: (x: any) => any,
} & HTMLAttributes<any>
type PropsStepToolButton = {
    focusKey?: UniqueFocusKey | null
    selectedValue: number
    values: number[]
    tooltip?: string | null
    uiTag?: string
    onSelect?: (x: any) => any,
} & HTMLAttributes<any>
type PropsSection = {
    title?: string | null
    uiTag?: string
    children: string | JSX.Element | JSX.Element[]
}

const registryIndex = {
    Section: ["game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx", "Section"],
    ToolButton: ["game-ui/game/components/tool-options/tool-button/tool-button.tsx", "ToolButton"],
    toolButtonTheme: ["game-ui/game/components/tool-options/tool-button/tool-button.module.scss", "classes"],
    StepToolButton: ["game-ui/game/components/tool-options/tool-button/tool-button.tsx", "StepToolButton"],
    mouseToolOptionsTheme: ["game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.module.scss", "classes"],
    FOCUS_DISABLED: ["game-ui/common/focus/focus-key.ts", "FOCUS_DISABLED"],
}

export class VanillaResolver {
    public static get instance(): VanillaResolver { return this._instance!! }
    private static _instance?: VanillaResolver

    public static setRegistry(in_registry: ModuleRegistry) { this._instance = new VanillaResolver(in_registry); }
    private registryData: ModuleRegistry;

    constructor(in_registry: ModuleRegistry) {
        this.registryData = in_registry;
    }

    private cachedData: Partial<Record<keyof typeof registryIndex, any>> = {}

    private updateCache(entry: keyof typeof registryIndex) {
        const entryData = registryIndex[entry];
        return this.cachedData[entry] = this.registryData.registry.get(entryData[0])!![entryData[1]]
    }

    public get Section(): (props: PropsSection) => JSX.Element { return this.cachedData["Section"] ?? this.updateCache("Section") }
    public get ToolButton(): (props: PropsToolButton) => JSX.Element { return this.cachedData["ToolButton"] ?? this.updateCache("ToolButton") }
    public get toolButtonTheme(): Theme | any { return this.cachedData["toolButtonTheme"] ?? this.updateCache("toolButtonTheme") }
    public get StepToolButton(): (props: PropsStepToolButton) => JSX.Element { return this.cachedData["StepToolButton"] ?? this.updateCache("StepToolButton") }
    public get mouseToolOptionsTheme(): Theme | any { return this.cachedData["mouseToolOptionsTheme"] ?? this.updateCache("mouseToolOptionsTheme") }
    public get FOCUS_DISABLED(): UniqueFocusKey { return this.cachedData["FOCUS_DISABLED"] ?? this.updateCache("FOCUS_DISABLED") }
}