export function formatSmart(value: number): string {
    return value.toFixed(2).replace(/\.?0+$/, "");
}

export function formatMeters(value: number): string {
    return `${formatSmart(value)} m`;
}
export function formatUnits(value: number): string {
    return `${formatSmart(value)} U`;
}
