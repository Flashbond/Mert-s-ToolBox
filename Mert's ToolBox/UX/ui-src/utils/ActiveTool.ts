export type ActiveTool = {
    id: string;
    name: string;
};

export function parseActiveTool(value: string | undefined | null): ActiveTool {
    const raw = value || "None|None";
    const parts = raw.split("|");

    return {
        id: parts[0] || "None",
        name: parts[1] || parts[0] || "None"
    };
}