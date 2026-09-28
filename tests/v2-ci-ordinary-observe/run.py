#!/usr/bin/env python3
from __future__ import annotations

import base64
import hashlib
import importlib.util
import json
import pathlib
import unittest
from unittest.mock import patch


ROOT = pathlib.Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "v2_ci_ordinary_observe", ROOT / "tools/v2-ci-ordinary-observe.py"
)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class SddObservationSourceTests(unittest.TestCase):
    def test_tools_match_recorded_repaired_source_digests(self):
        policy = json.loads((ROOT / "policy/v2-ci-ordinary-settlement.json").read_text())
        expected = {
            "tools/v2-ci-ordinary-observe.py": policy["sourceImplementation"]["observerSha256"],
            "tools/v2-ci-ordinary-qualification.py":
                policy["sourceImplementation"]["qualificationSha256"],
        }
        for relative, digest in expected.items():
            self.assertEqual(digest, hashlib.sha256((ROOT / relative).read_bytes()).hexdigest())

    def test_sdd_profile_is_fixed_to_exact_checks_and_producers(self):
        policy = json.loads((ROOT / "policy/v2-ci-ordinary-settlement.json").read_text())
        profile = MODULE.QUALIFICATION.source_profile(policy, "sdd-v1")
        self.assertEqual("FS-GG/FS.GG.SDD", profile["repository"])
        self.assertEqual(1274272672, profile["repositoryId"])
        self.assertEqual(15368, profile["requiredCheckAppId"])
        self.assertEqual({'Shared-build-config drift check', 'Deterministic gate (locked restore + build + test)'},
                         set(profile["requiredChecks"]))
        self.assertEqual(set(policy["qualification"]["requiredGateChecks"]),
                         set(profile["requiredGateChecks"]))
        self.assertEqual(profile["requiredChecks"], policy["qualification"]["requiredChecks"])
        self.assertEqual(profile["checkProducers"], policy["qualification"]["checkProducers"])
        self.assertEqual(
            {
                "Deterministic gate (locked restore + build + test)": 303425212,
                "kit / coordination-kit": 307166760,
                "Shared-build-config drift check": 303425212,
                "materialize / receiver-validate": 316861917,
                "API compatibility gate (breaking-change → SemVer major)": 303425212,
                "skill-view-check": 321983347,
            },
            {name: producer["workflowId"]
             for name, producer in profile["checkProducers"].items()},
        )
        with patch.object(MODULE.QUALIFICATION, "read_json", return_value=policy):
            with self.assertRaisesRegex(MODULE.QUALIFICATION.Refusal, "no rehearsal activation"):
                MODULE.observe({"FSGG_V2_SOURCE_PROFILE": "sdd-v1"}, rehearsal=True)

    def test_sdd_local_policy_is_admitted_before_runtime_event_fences(self):
        policy = json.loads((ROOT / "policy/v2-ci-ordinary-settlement.json").read_text())
        env = {
            "FSGG_V2_SOURCE_PROFILE": "sdd-v1",
            "GITHUB_SHA": "a" * 40,
            "GITHUB_REPOSITORY": "FS-GG/FS.GG.SDD",
            "GITHUB_EVENT_NAME": "pull_request",
            "GITHUB_REF": "refs/heads/main",
        }
        repository = {
            "id": 1274272672,
            "full_name": "FS-GG/FS.GG.SDD",
            "default_branch": "main",
        }
        with patch.object(MODULE.QUALIFICATION, "read_json", return_value=policy), \
                patch.object(MODULE, "api", return_value=repository):
            with self.assertRaisesRegex(MODULE.QUALIFICATION.Refusal,
                                        "pinned protected-main event"):
                MODULE.observe(env)

    def test_current_authority_reads_sdd_policy_and_workflow_from_one_main_revision(self):
        policy = json.loads((ROOT / "policy/v2-ci-ordinary-settlement.json").read_text())
        revision = "a" * 40

        def api(path):
            if path == "repos/FS-GG/FS.GG.SDD/git/ref/heads/main":
                return {"object": {"sha": revision}}
            prefix = "repos/FS-GG/FS.GG.SDD/contents/"
            if path.startswith(prefix) and path.endswith("?ref=" + revision):
                relative = path[len(prefix):].split("?ref=", 1)[0]
                return {
                    "encoding": "base64",
                    "content": base64.b64encode((ROOT / relative).read_bytes()).decode(),
                }
            raise AssertionError(path)

        with patch.object(MODULE, "api", side_effect=api):
            MODULE.current_authority("FS-GG/FS.GG.SDD", policy)

    def test_settlement_binds_published_package_and_exact_receipt(self):
        workflow = (ROOT / ".github/workflows/v2-ci-ordinary-settlement.yml").read_text()
        self.assertIn("  push:\n    branches: [main]", workflow)
        self.assertNotIn("    if: ${{ false }}", workflow)
        self.assertIn("if: needs.preflight.outputs.activation == 'true'", workflow)
        self.assertIn("environment: ordinary-v2", workflow)
        self.assertIn("FSGG_V2_SOURCE_PROFILE: sdd-v1", workflow)
        self.assertEqual(2, workflow.count("persist-credentials: false"))
        self.assertIn("python3 tools/v2-ci-ordinary-observe.py produce", workflow)
        self.assertIn("python3 tools/v2-ci-ordinary-observe.py verify", workflow)
        self.assertIn("EXPECTED_RECEIPT_SHA256: ${{ needs.preflight.outputs.receipt_sha256 }}", workflow)
        self.assertIn("PACKAGE_VERSION: 0.1.6", workflow)
        self.assertIn("PACKAGE_SHA256: 0f5d92799af84acb8663df0f524dc2ccfe54cfcc0bc6ad2183e867c8cdd47730", workflow)
        self.assertIn("--configfile", workflow)
        self.assertIn("--no-cache", workflow)
        self.assertIn("ordinary-settlement execute", workflow)
        for name in ("V2_ORDINARY_APP_ID", "V2_ORDINARY_APP_PRIVATE_KEY",
                     "V2_ORDINARY_AUTHORIZER_PRIVATE_KEY"):
            self.assertIn("${{ secrets." + name + " }}", workflow)
        for forbidden in ("workflow_dispatch:", "repository_dispatch:", "pull_request:",
                          "pull_request_target:", "V1_ADMISSION", "CALLABLE_ISOLATED_OPERATION"):
            self.assertNotIn(forbidden, workflow)

    def test_policy_anchor_environment_and_verified_package_are_bounded(self):
        policy = json.loads((ROOT / "policy/v2-ci-ordinary-settlement.json").read_text())
        anchor = json.loads((ROOT / "policy/v2-ci-ordinary-settlement-anchor.json").read_text())
        self.assertEqual("v2-ci-i1-ordinary-settlement-v1", policy["policyId"])
        self.assertEqual(policy["policyId"], anchor["policyId"])
        self.assertEqual("installed", policy["status"])
        self.assertTrue(policy["credentialJob"]["installed"])
        observation = policy["credentialJob"]["liveObservation"]
        self.assertEqual(22938103320, observation["environmentId"])
        self.assertEqual(61311037, observation["branchPolicyId"])
        self.assertEqual("main", observation["customBranchPolicy"])
        self.assertEqual(3, observation["secretCount"])
        self.assertEqual({"V2_ORDINARY_APP_ID", "V2_ORDINARY_APP_PRIVATE_KEY",
                          "V2_ORDINARY_AUTHORIZER_PRIVATE_KEY"}, set(observation["secretNames"]))
        self.assertEqual("published-verified",
                         policy["packagePin"]["status"])
        self.assertEqual("0.1.6", policy["packagePin"]["version"])
        self.assertEqual("0f5d92799af84acb8663df0f524dc2ccfe54cfcc0bc6ad2183e867c8cdd47730", policy["packagePin"]["sha256"])
        self.assertTrue(policy["packagePin"]["servedPackageVerified"])
        self.assertEqual(3, len(policy["credentialInventory"]))
        self.assertTrue(all(item["provisioned"] for item in policy["credentialInventory"]))
        self.assertEqual(5064713, anchor["writer"]["appId"])
        self.assertEqual(164553252, anchor["writer"]["installationId"])
        self.assertEqual(1351660651, anchor["writer"]["repositoryId"])


if __name__ == "__main__":
    unittest.main()
