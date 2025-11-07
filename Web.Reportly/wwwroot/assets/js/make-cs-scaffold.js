#!/usr/bin/env node
// Node 16+
// Usage:
//   node make-cs-scaffold.js <FolderName> [--base <BaseName>] [--nsroot <NsRoot>] [--out <dir>] [--force]
// Ví dụ đúng như bạn muốn:
//   node make-cs-scaffold.js Uploads --base Upload --nsroot Service.Reportly --out Service.Reportly --force

const fs = require("fs");
const fsp = require("fs/promises");
const path = require("path");

const args = process.argv.slice(2);
if (!args[0]) {
    console.error("Thiếu <FolderName>. Ví dụ: node make-cs-scaffold.js Uploads --base Upload");
    process.exit(1);
}
const folderNameRaw = args[0];

function opt(flag, def = null) {
    const i = args.indexOf(flag);
    return i !== -1 && args[i + 1] ? args[i + 1] : def;
}
const baseNameRaw = opt("--base", folderNameRaw);
const nsRoot = opt("--nsroot", "Service.Reportly"); // tầng service theo yêu cầu
const outOpt = opt("--out", null);
const force = args.includes("--force");

function toPascal(s) {
    return (s || "")
        .replace(/[_\-\.\s]+/g, " ")
        .trim()
        .split(" ")
        .filter(Boolean)
        .map(w => w[0].toUpperCase() + w.slice(1))
        .join("");
}

const cwd = process.cwd();
const baseOutDir =
    outOpt ? path.resolve(cwd, outOpt)
        : (fs.existsSync(path.join(cwd, "Service.Reportly")) ? path.join(cwd, "Service.Reportly") : cwd);

const folderName = toPascal(folderNameRaw);
const baseName = toPascal(baseNameRaw);
const namespaceFull = `${nsRoot}.${folderName}`;

const targetDir = path.join(baseOutDir, folderName);

const suffixes = ["One", "Model", "Many", "Command"];

function csTemplate(className, ns) {
    return `namespace ${ns}
{
    public class ${className}
    {

    }
}
`;
}

(async () => {
    try {
        await fsp.mkdir(targetDir, { recursive: true });
        for (const sfx of suffixes) {
            const className = `${baseName}${sfx}`;
            const fp = path.join(targetDir, `${className}.cs`);
            if (fs.existsSync(fp) && !force) {
                console.warn(`⚠️  Tồn tại: ${path.relative(cwd, fp)} (dùng --force để ghi đè)`);
                continue;
            }
            await fsp.writeFile(fp, csTemplate(className, namespaceFull), "utf8");
            console.log(`✅ Tạo: ${path.relative(cwd, fp)}`);
        }

        console.log(`\n📁 Thư mục: ${path.relative(cwd, targetDir)}`);
        console.log(`🧩 Namespace: ${namespaceFull}`);
    } catch (e) {
        console.error("Lỗi:", e.message);
        process.exit(1);
    }
})();
