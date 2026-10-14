import { Button } from "cs2/ui";
import { ReactNode } from "react";
import classNames from "classnames";
import styles from "shared/TextButton.module.scss";

interface TextButtonProps {
    children: ReactNode;
    onSelect?: () => void;
    disabled?: boolean;
    selected?: boolean;
    tooltipLabel?: ReactNode;
}

// Flattened replacement for a plain `<Button>` - see base.scss's
// flatButtonChrome for why. One wrapper, not a className added at every
// call site, since this chrome is still being dialed in via in-game
// screenshots and is easier to adjust from one place while that's true.
export const TextButton = ({ children, onSelect, disabled, selected, tooltipLabel }: TextButtonProps) => (
    <Button
        variant="flat"
        selected={selected}
        disabled={disabled}
        tooltipLabel={tooltipLabel}
        onSelect={onSelect}
        className={classNames(styles.button, selected && styles.selected)}
    >
        {children}
    </Button>
);
