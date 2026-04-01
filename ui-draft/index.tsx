import { ModRegistrar } from "cs2/modding";
import { PerfectCirclePanel } from "./PerfectCirclePanel";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.append("Game", PerfectCirclePanel);
};

export default register;