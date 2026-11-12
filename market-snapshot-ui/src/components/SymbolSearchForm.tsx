import { useState } from "react";
type Props = { onSearch: (symbol: string) => void };

export default function SymbolSearchForm({ onSearch }: Props) {
    const [symbol, setSymbol] = useState("");

    const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
        e.preventDefault();
        onSearch(symbol.trim())
    };

    return (
        <form onSubmit={handleSubmit}>
            <label htmlFor="symbol">Stock symbol</label>
            <input
                id="symbol"
                value={symbol}
                onChange={(e) => setSymbol(e.target.value)}
                placeholder="Enter a ticker"
                maxLength={10}
            />
            <button type="submit" disabled={!symbol.trim()}>
                Search
            </button>
        </form>
    );
}