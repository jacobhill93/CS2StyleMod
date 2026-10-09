import * as cs2ui from "cs2/ui";
import { Dropdown, DropdownToggle } from "cs2/ui";
import { ReactNode } from "react";

// cs2/ui's own ambient type declarations (UI/types/ui.d.ts) declare both a
// plain data interface `DropdownItem<T>` (item shape) and re-export the
// real component under that same name from its internal name
// `DropdownItem$1` - the two collide, and the named import only resolves
// to the type half ("only refers to a type, but is being used as a value
// here"). The component itself is real (same runtime window global every
// other cs2/ui import resolves to) - this just works around the broken
// named-export typing for it specifically.
const DropdownItem = cs2ui.DropdownItem as unknown as (props: {
    value: string;
    selected?: boolean;
    onChange?: (value: string) => void;
    children?: ReactNode;
}) => JSX.Element;

// Empty string means "any"/"none selected", shared across every user of
// this component so callers compare against the same sentinel.
export const ANY = "";

export interface FilterDropdownProps {
    label: string;
    options: string[];
    value: string;
    onChange: (value: string) => void;
    formatOption?: (value: string) => string;
}

// cs2/ui has no native <select> - raw <select>/<option> crashed the whole
// game UI render tree in-game (confirmed via UI.log: a TypeError inside the
// game's own core bundle, not ours - there's no error boundary around an
// individually appended mod panel, so one throw there takes down the
// entire HUD, see DECISIONS.md). Dropdown/DropdownToggle/DropdownItem is
// the actual supported mechanism for "select one of several mutually
// exclusive values" - shared here since the building picker, and now the
// zones panel, both need it.
export const FilterDropdown = ({ label, options, value, onChange, formatOption }: FilterDropdownProps) => {
    const format = formatOption ?? ((v: string) => v);
    return (
        <Dropdown
            content={
                <>
                    <DropdownItem value={ANY} selected={value === ANY} onChange={onChange}>{label}</DropdownItem>
                    {options.map((option) => (
                        <DropdownItem key={option} value={option} selected={value === option} onChange={onChange}>
                            {format(option)}
                        </DropdownItem>
                    ))}
                </>
            }
        >
            <DropdownToggle>{value === ANY ? label : format(value)}</DropdownToggle>
        </Dropdown>
    );
};
