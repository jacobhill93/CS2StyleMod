import { MouseEvent as ReactMouseEvent } from "react";
import styles from "./ResizeHandle.module.scss";

interface ResizeHandleProps {
    onMouseDown: (event: ReactMouseEvent) => void;
}

// Small corner grip, anchored to its nearest positioned ancestor - the
// outer position:absolute wrapper div each panel renders inside (see
// useDraggable's usage sites), confirmed working in-game.
export const ResizeHandle = ({ onMouseDown }: ResizeHandleProps) => (
    <div onMouseDown={onMouseDown} className={styles.handle} />
);
