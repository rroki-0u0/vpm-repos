// GitHub Releases から VPM リスティング (index.json) を生成し、site/ と合わせて dist/ に出力する。
// 各リリースには release.yml が添付した package.json / <zip> / <zip>.sha256 が必要。
import { cp, mkdir, readFile, writeFile } from "node:fs/promises";

const repo = process.env.REPO ?? "rroki-0u0/vpm-repos";
const token = process.env.GITHUB_TOKEN;

const config = JSON.parse(await readFile("listing.config.json", "utf8"));

const apiHeaders = {
  Accept: "application/vnd.github+json",
  "X-GitHub-Api-Version": "2022-11-28",
  ...(token ? { Authorization: `Bearer ${token}` } : {}),
};

async function apiJson(url) {
  const res = await fetch(url, { headers: apiHeaders });
  if (!res.ok) throw new Error(`GitHub API ${res.status}: ${url}`);
  return res.json();
}

// 公開リポジトリ前提: アセットは browser_download_url を素の fetch で取得する
async function assetText(asset) {
  const res = await fetch(asset.browser_download_url);
  if (!res.ok) throw new Error(`asset ${res.status}: ${asset.browser_download_url}`);
  return res.text();
}

async function listAllReleases() {
  const releases = [];
  for (let page = 1; ; page++) {
    const chunk = await apiJson(
      `https://api.github.com/repos/${repo}/releases?per_page=100&page=${page}`
    );
    releases.push(...chunk);
    if (chunk.length < 100) break;
  }
  return releases;
}

const packages = {};
for (const release of await listAllReleases()) {
  if (release.draft || release.prerelease) continue;

  const packageJsonAsset = release.assets.find((a) => a.name === "package.json");
  if (!packageJsonAsset) {
    console.warn(`skip ${release.tag_name}: package.json asset not found`);
    continue;
  }
  let pkg;
  try {
    pkg = JSON.parse(await assetText(packageJsonAsset));
  } catch (e) {
    console.warn(`skip ${release.tag_name}: ${e.message}`);
    continue;
  }

  const zipName = `${pkg.name}-${pkg.version}.zip`;
  const zipAsset = release.assets.find((a) => a.name === zipName);
  if (!zipAsset) {
    console.warn(`skip ${release.tag_name}: ${zipName} not found`);
    continue;
  }

  const entry = { ...pkg, url: zipAsset.browser_download_url };

  const shaAsset = release.assets.find((a) => a.name === `${zipName}.sha256`);
  if (shaAsset) {
    const sha = (await assetText(shaAsset)).trim().split(/\s+/)[0];
    if (/^[0-9a-f]{64}$/i.test(sha)) entry.zipSHA256 = sha;
  }

  packages[pkg.name] ??= { versions: {} };
  packages[pkg.name].versions[pkg.version] = entry;
  console.log(`listed: ${pkg.name} ${pkg.version}`);
}

const listing = {
  name: config.name,
  id: config.id,
  url: config.url,
  author: config.author,
  packages,
};

await mkdir("dist", { recursive: true });
await cp("site", "dist", { recursive: true });
await writeFile("dist/index.json", JSON.stringify(listing, null, 2) + "\n");
console.log(`done: ${Object.keys(packages).length} package(s)`);
