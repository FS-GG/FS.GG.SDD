import fs from "node:fs"
import { pathToFileURL } from "node:url"

const [schemaPath, contractPath] = process.argv.slice(2)
if (!schemaPath || !contractPath || !process.env.AJV_ROOT) {
  console.error("binding=compiled-contract usage: AJV_ROOT=... node validate-contract.mjs <schema> <contract>")
  process.exit(2)
}

const ajvModule = await import(pathToFileURL(`${process.env.AJV_ROOT}/node_modules/ajv/dist/2020.js`))
const Ajv2020 = ajvModule.default
const schema = JSON.parse(fs.readFileSync(schemaPath, "utf8"))
const contract = JSON.parse(fs.readFileSync(contractPath, "utf8"))
const ajv = new Ajv2020({ allErrors: true, strict: true })
const validate = ajv.compile(schema)

if (!validate(contract)) {
  for (const error of validate.errors ?? []) {
    console.error(`binding=compiled-contract schemaPath=${error.instancePath || "/"} keyword=${error.keyword} message=${error.message}`)
  }
  process.exit(1)
}
