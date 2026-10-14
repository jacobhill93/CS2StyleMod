import classNames from "classnames";
import { TextInputTheme } from "vanilla/TextInputTheme";
import styles from "shared/TextInput.module.scss";

interface TextInputProps {
    value: string;
    onChange: (value: string) => void;
    placeholder?: string;
    className?: string;
}

// Coherent Gameface doesn't render the native `placeholder` attribute -
// confirmed in-game 2026-10-13 (every one of this mod's 4 placeholder
// usages showed as a blank box, not hint text). Overlaid <span> instead,
// shown only while the input is empty - built once here so the fix
// applies everywhere instead of 4 separately hand-rolled copies.
//
// `className` lands on the wrapper, not the `<input>` - every call site
// uses it for layout among siblings (flex: 1, width: 100%, margin), and
// that has to apply to whichever element actually occupies space in the
// surrounding flex/block context, which is now this wrapper.
export const TextInput = ({ value, onChange, placeholder, className }: TextInputProps) => (
    <div className={classNames(styles.wrapper, className)}>
        <input value={value} onChange={(event) => onChange(event.target.value)} className={classNames(TextInputTheme.input, styles.input)} />
        {value === "" && placeholder && <span className={styles.placeholder}>{placeholder}</span>}
    </div>
);
