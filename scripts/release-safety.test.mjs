import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { chmodSync, mkdirSync, mkdtempSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import test from 'node:test';
import yaml from 'js-yaml';
import { fileURLToPath } from 'node:url';
import releaseConfig from '../.releaserc.mjs';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const SCRIPT = join(ROOT, 'scripts/generate-badges.mjs');
const CLOUDFLARE_WORKER_SOURCE = readFileSync(
  join(ROOT, 'infra/cloudflare/worker/src/index.ts'),
  'utf8',
);
const ACA_TERRAFORM_SOURCE = readFileSync(
  join(ROOT, 'infra/terraform/modules/aca-platform/main.tf'),
  'utf8',
);
const WORKFLOWS = Object.fromEntries(
  [
    'beta-release',
    'stable-release',
    'container-build',
    'azure-container-release',
    'cloudflare-container-release',
    'pr',
    'codeql',
    'infra-deploy',
  ].map((name) => {
    const path = join(ROOT, `.github/workflows/${name}.yml`);
    const source = readFileSync(path, 'utf8');
    return [name, { source, workflow: yaml.load(source) }];
  }),
);
const PROVENANCE_FIELDS = [
  'api-image-archive-sha256',
  'worker-image-archive-sha256',
  'container-archives-sha256',
  'node-version',
  'npm-version',
  'dotnet-sdk-version',
  'dotnet-sdks',
  'dotnet-runtimes',
  'runner-os',
  'runner-arch',
  'runner-image-os',
  'runner-image-version',
];

function git(cwd, args) {
  return execFileSync('git', args, { cwd, encoding: 'utf8' }).trim();
}

function remoteGit(remote, args) {
  return execFileSync('git', ['--git-dir', remote, ...args], { encoding: 'utf8' }).trim();
}

function createRepository() {
  const directory = mkdtempSync(join(tmpdir(), 'release-safety-'));
  const remote = join(directory, 'remote.git');
  const repo = join(directory, 'candidate');
  execFileSync('git', ['init', '--bare', '--initial-branch=main', remote], { encoding: 'utf8' });
  execFileSync('git', ['init', '-b', 'main', repo], { encoding: 'utf8' });
  git(repo, ['config', 'core.autocrlf', 'false']);
  git(repo, ['config', 'user.name', 'Release safety test']);
  git(repo, ['config', 'user.email', 'release-safety@example.invalid']);
  mkdirSync(join(repo, 'badges'));
  writeFileSync(join(repo, 'README.md'), 'validated candidate\n');
  writeFileSync(join(repo, 'badges/release.png'), Buffer.from('stable badge'));
  writeFileSync(join(repo, 'badges/pre-release.png'), Buffer.from('old beta badge'));
  git(repo, ['add', '.']);
  git(repo, ['commit', '-m', 'test: validated candidate']);
  const validatedSha = git(repo, ['rev-parse', 'HEAD']);
  git(repo, ['remote', 'add', 'origin', remote]);
  git(repo, ['push', '-u', 'origin', 'main']);
  return { directory, remote, repo, validatedSha };
}

function runBadgePrepare({ repo, validatedSha, outputPath }) {
  return spawnSync(process.execPath, [SCRIPT, '3.2.1-alpha.1', '--publish'], {
    cwd: repo,
    encoding: 'utf8',
    env: {
      ...process.env,
      BADGE_BRANCH: 'main',
      GITHUB_OUTPUT: outputPath,
      VALIDATED_SHA: validatedSha,
    },
  });
}

function remoteBranchSha(remote, branch = 'main') {
  return remoteGit(remote, ['rev-parse', `refs/heads/${branch}`]);
}

test('workflow YAML preserves triggers, read-only fork README sync, and stable deployment ordering', () => {
  const stable = WORKFLOWS['stable-release'].workflow;
  const beta = WORKFLOWS['beta-release'].workflow;
  const pr = WORKFLOWS.pr.workflow;

  assert.ok(Object.hasOwn(stable.on, 'workflow_dispatch'));
  assert.deepEqual(beta.on.push.branches, ['main']);
  assert.deepEqual(pr.on.pull_request.branches, ['main']);
  assert.ok(stable.jobs['deploy-production'].needs.includes('sync-to-main'));
  assert.deepEqual(pr.permissions, { contents: 'read' });
  assert.deepEqual(stable.jobs.release.permissions, { contents: 'read' });
  assert.deepEqual(stable.jobs['deploy-production'].permissions, {
    'id-token': 'write',
    contents: 'read',
    actions: 'read',
  });
  assert.deepEqual(beta.jobs.release.permissions, { contents: 'read' });
  assert.deepEqual(beta.jobs['deploy-testing'].permissions, {
    'id-token': 'write',
    contents: 'read',
    actions: 'read',
  });
  assert.equal(stable.jobs['deploy-production'].environment.name, 'production');
  assert.equal(beta.jobs['deploy-testing'].environment.name, 'testing');
  for (const [workflow, appServiceJob, azureContainerJob, cloudflareContainerJob] of [
    [beta, 'deploy-testing', 'deploy-testing-azure-container', 'deploy-testing-cloudflare-container'],
    [
      stable,
      'deploy-production',
      'deploy-production-azure-container',
      'deploy-production-cloudflare-container',
    ],
  ]) {
    const targetValidation = workflow.jobs.validate.steps.find(
      (step) => step.name === '🔎 Validate container deployment target',
    );
    assert.equal(
      targetValidation.env.CONFIGURED_TARGET,
      '${{ vars.CONTAINER_DEPLOYMENT_TARGET }}',
    );
    assert.equal(
      workflow.jobs.validate.outputs['container-deployment-target'],
      '${{ steps.deployment-target.outputs.target }}',
    );
    assert.match(targetValidation.run, /CONFIGURED_TARGET:-app-service/);
    assert.match(targetValidation.run, /app-service\|azure-container-apps\|cloudflare-containers/);
    assert.equal(workflow.jobs[appServiceJob].if.includes("container-deployment-target == 'app-service'"), true);
    assert.equal(
      workflow.jobs[azureContainerJob].uses,
      './.github/workflows/azure-container-release.yml',
    );
    assert.equal(
      workflow.jobs[cloudflareContainerJob].uses,
      './.github/workflows/cloudflare-container-release.yml',
    );
    assert.match(workflow.jobs[azureContainerJob].if, /azure-container-apps/);
    assert.match(workflow.jobs[cloudflareContainerJob].if, /cloudflare-containers/);
    assert.equal(
      workflow.jobs[azureContainerJob].with.release_tag,
      '${{ needs.release.outputs.new-release-tag }}',
    );
    assert.equal(
      workflow.jobs[cloudflareContainerJob].with.release_tag,
      '${{ needs.release.outputs.new-release-tag }}',
    );
    assert.equal(workflow.jobs[azureContainerJob].with.target_environment, workflow === beta ? 'testing' : 'production');
    assert.equal(
      workflow.jobs[cloudflareContainerJob].with.target_environment,
      workflow === beta ? 'testing' : 'production',
    );
  }
  assert.equal(
    beta.jobs['deploy-testing'].steps.find((step) => step.name === '🚀 Deploy to Azure Web App (Testing)').uses,
    'azure/webapps-deploy@02a81bead70021f5284939794bcec79c271ab383',
  );
  assert.equal(
    stable.jobs['deploy-production'].steps.find((step) => step.name === '🚀 Deploy to Azure Web App (Production)').uses,
    'azure/webapps-deploy@02a81bead70021f5284939794bcec79c271ab383',
  );
  assert.doesNotMatch(
    `${WORKFLOWS['beta-release'].source}\n${WORKFLOWS['stable-release'].source}`,
    /az containerapp|wrangler deploy/,
  );

  const readmeSync = pr.jobs['sync-readme-versions'];
  assert.match(readmeSync.if, /head\.repo\s*!=\s*null/);
  assert.deepEqual(readmeSync.permissions, { contents: 'read' });
  assert.equal(readmeSync.steps[0].with.repository, '${{ github.event.pull_request.head.repo.full_name }}');
  assert.equal(readmeSync.steps[0].with.ref, '${{ github.event.pull_request.head.ref }}');
  assert.equal(readmeSync.steps[0].with['persist-credentials'], false);
});

test('container build workflow validates an exact tag and records OCI archive provenance', () => {
  const { source, workflow } = WORKFLOWS['container-build'];
  const build = workflow.jobs.build;
  const verify = build.steps.find((step) => step.id === 'verify-source');
  const publish = build.steps.find((step) => step.id === 'publish');
  const upload = build.steps.find((step) => step.name === '⬆️ Upload container build artifact');

  assert.deepEqual(Object.keys(workflow.on), ['workflow_call']);
  assert.equal(workflow.on.workflow_call.inputs.release_tag.required, true);
  assert.equal(workflow.on.workflow_call.inputs.target_environment.required, true);
  assert.equal(workflow.env.NODE_VERSION, '24.20.0');
  assert.equal(workflow.env.DOTNET_VERSION, '10.0.303');
  assert.ok(verify.run.includes('refs/tags/$RELEASE_TAG'));
  assert.ok(verify.run.includes('testing:alpha|production:stable'));
  assert.ok(verify.run.includes('refs/remotes/origin/main'));
  assert.ok(verify.run.includes('refs/remotes/origin/release'));
  assert.ok(publish);
  assert.ok(upload);

  for (const field of PROVENANCE_FIELDS) {
    assert.equal(build.outputs[field], `\${{ steps.publish.outputs.${field} }}`);
    assert.equal(workflow.on.workflow_call.outputs[field].value, `\${{ jobs.build.outputs.${field} }}`);
    assert.ok(publish.run.includes(`${field}=%s`), `container build must record ${field}`);
  }
  assert.match(publish.run, /\/t:PublishContainer/);
  assert.match(publish.run, /ContainerArchiveOutputPath=.*ref-test-management-api\.tar\.gz/);
  assert.match(publish.run, /ContainerArchiveOutputPath=.*ref-test-management-worker\.tar\.gz/);
  assert.match(publish.run, /API_IMAGE_ARCHIVE_SHA256=.*sha256sum/);
  assert.match(publish.run, /WORKER_IMAGE_ARCHIVE_SHA256=.*sha256sum/);
  assert.match(publish.run, /CONTAINER_ARCHIVES_SHA256=.*find \. -type f -print0 \| LC_ALL=C sort -z/);
  assert.doesNotMatch(publish.run, /docker (?:build|push)/);
  assert.equal(upload.with['retention-days'], 1);
  assert.match(source, /node-version: \$\{\{ env\.NODE_VERSION \}\}/);
});

test('provider container releases are opt-in and verify artifacts before deployment', () => {
  for (const name of ['azure-container-release', 'cloudflare-container-release']) {
    const { workflow } = WORKFLOWS[name];
    const deploy = workflow.jobs.deploy;
    const verify = deploy.steps.find((step) => step.name === '🔒 Verify tagged build provenance and archives');

    assert.deepEqual(Object.keys(workflow.on), ['workflow_call']);
    assert.equal(workflow.on.workflow_call.inputs.release_tag.required, true);
    assert.equal(workflow.on.workflow_call.inputs.target_environment.required, true);
    assert.equal(workflow.on.workflow_call.inputs.release_tag.type, 'string');
    assert.equal(workflow.jobs.build.uses, './.github/workflows/container-build.yml');
    assert.ok(verify);
    assert.equal(verify.env.VALIDATED_SHA, '${{ needs.build.outputs.validated-sha }}');
    assert.equal(verify.env.API_IMAGE_ARCHIVE_SHA256, '${{ needs.build.outputs.api-image-archive-sha256 }}');
    assert.equal(verify.env.WORKER_IMAGE_ARCHIVE_SHA256, '${{ needs.build.outputs.worker-image-archive-sha256 }}');
    assert.equal(verify.env.CONTAINER_ARCHIVES_SHA256, '${{ needs.build.outputs.container-archives-sha256 }}');
    assert.match(verify.run, /ACTUAL_API_IMAGE_ARCHIVE_SHA256=.*sha256sum/);
    assert.match(verify.run, /ACTUAL_WORKER_IMAGE_ARCHIVE_SHA256=.*sha256sum/);
    assert.match(verify.run, /ACTUAL_CONTAINER_ARCHIVES_SHA256=.*find \. -type f -print0 \| LC_ALL=C sort -z/);
  }

  const azure = WORKFLOWS['azure-container-release'].workflow.jobs.deploy;
  const cloudflare = WORKFLOWS['cloudflare-container-release'].workflow.jobs.deploy;
  assert.equal(azure.environment.name, '${{ inputs.target_environment }}');
  assert.equal(cloudflare.environment.name, 'cloudflare-${{ inputs.target_environment }}');
  assert.deepEqual(azure.permissions, {
    'id-token': 'write',
    contents: 'read',
    actions: 'read',
  });
  assert.deepEqual(cloudflare.permissions, { contents: 'read', actions: 'read' });
  assert.ok(
    azure.steps.findIndex((step) => step.name === '🔒 Verify tagged build provenance and archives') <
      azure.steps.findIndex((step) => step.name === '🔑 Azure Login'),
  );
  const cloudflareVerifyIndex = cloudflare.steps.findIndex(
    (step) => step.name === '🔒 Verify tagged build provenance and archives',
  );
  const cloudflareValidationIndex = cloudflare.steps.findIndex(
    (step) => step.name === '🔎 Validate Cloudflare Environment',
  );
  const cloudflareCredentialIndex = cloudflare.steps.findIndex(
    (step) => step.name === '🔑 Validate Wrangler credentials',
  );
  const migrationIndex = cloudflare.steps.findIndex(
    (step) => step.name === '🐳 Load tagged Worker image and run migration',
  );
  const imagePushIndex = cloudflare.steps.findIndex(
    (step) => step.name === '🐳 Push tagged images and resolve immutable digests',
  );
  const cloudflareDeployIndex = cloudflare.steps.findIndex(
    (step) => step.name === '🚀 Deploy Worker and immutable Containers',
  );
  assert.ok(
    cloudflareVerifyIndex < cloudflareValidationIndex &&
      cloudflareValidationIndex < cloudflareCredentialIndex &&
      cloudflareCredentialIndex < migrationIndex &&
      migrationIndex < imagePushIndex &&
      imagePushIndex < cloudflareDeployIndex,
  );
  assert.equal(cloudflare.env.CLOUDFLARE_API_TOKEN, undefined);
  assert.equal(
    cloudflare.steps[cloudflareCredentialIndex].env.CLOUDFLARE_API_TOKEN,
    '${{ secrets.CLOUDFLARE_API_TOKEN }}',
  );
  assert.equal(
    cloudflare.steps[imagePushIndex].env.CLOUDFLARE_API_TOKEN,
    '${{ secrets.CLOUDFLARE_API_TOKEN }}',
  );
  assert.equal(
    cloudflare.steps[cloudflareDeployIndex].env.CLOUDFLARE_API_TOKEN,
    '${{ secrets.CLOUDFLARE_API_TOKEN }}',
  );
  assert.match(cloudflare.steps[imagePushIndex].run, /docker manifest inspect -v/);
  assert.match(cloudflare.steps[cloudflareDeployIndex].run, /--secrets-file/);
  assert.match(cloudflare.steps[cloudflareDeployIndex].run, /--containers-rollout gradual/);
});

test('release deployments push immutable images and finish migrations before Container Apps updates', () => {
  const deployJob = WORKFLOWS['azure-container-release'].workflow.jobs.deploy;
  const steps = deployJob.steps;
  const provenanceIndex = steps.findIndex((step) => step.name === '🔒 Verify tagged build provenance and archives');
  const loginIndex = steps.findIndex((step) => step.name === '🔑 Azure Login');
  const prepareIndex = steps.findIndex((step) => step.name === '🐳 Push immutable images and run migration');
  const deployIndex = steps.findIndex((step) => step.name === '🚀 Deploy immutable Container Apps revisions');
  const prepare = steps[prepareIndex];
  const deploy = steps[deployIndex];
  assert.ok(provenanceIndex < loginIndex && loginIndex < prepareIndex && prepareIndex < deployIndex);
  assert.match(prepare.run, /docker load --input .*ref-test-management-api\.tar\.gz/);
  assert.match(prepare.run, /docker push "\$API_TAGGED_IMAGE"/);
  assert.match(prepare.run, /docker push "\$WORKER_TAGGED_IMAGE"/);
  assert.match(prepare.run, /API_IMAGE="\$\{ACR_LOGIN_SERVER\}\/ref-test-management-api@\$\{API_DIGEST\}"/);
  assert.match(prepare.run, /WORKER_IMAGE="\$\{ACR_LOGIN_SERVER\}\/ref-test-management-worker@\$\{WORKER_DIGEST\}"/);

  const migrationUpdateIndex = prepare.run.indexOf('az containerapp job update');
  const migrationStartIndex = prepare.run.indexOf('az containerapp job start');
  const migrationStatusIndex = prepare.run.indexOf('az containerapp job execution show');
  assert.ok(migrationUpdateIndex >= 0 && migrationUpdateIndex < migrationStartIndex);
  assert.ok(migrationStartIndex < migrationStatusIndex);
  assert.match(prepare.run, /Succeeded/);
  assert.match(prepare.run, /Failed\|Stopped/);
  assert.match(prepare.run, /Migration execution did not complete/);
  assert.match(deploy.run, /--image "\$API_IMAGE"/);
  assert.match(deploy.run, /--image "\$WORKER_IMAGE"/);
  assert.match(deploy.run, /AZURE_DEPLOYMENT_MODE" = "split"/);
  assert.equal(deployJob.environment.url, '${{ steps.deploy.outputs.api-url }}');
  assert.match(deploy.run, /api-url=https:\/\/\$API_FQDN/);
});

test('PR validates SDK container archives and all Terraform roots without cloud credentials', () => {
  const { source, workflow } = WORKFLOWS.pr;
  const publishJob = workflow.jobs.validate;
  const publish = publishJob.steps.find((step) => step.name === '📦 Validate SDK container publishing');
  const terraformJob = workflow.jobs['validate-infrastructure'];

  assert.ok(publish);
  assert.deepEqual(workflow.permissions, { contents: 'read' });
  assert.deepEqual(terraformJob.permissions, { contents: 'read' });
  assert.match(publish.run, /dotnet publish RefTestManagement\.Api\/.*\/t:PublishContainer/s);
  assert.match(publish.run, /ContainerArchiveOutputPath=.*api-image\.tar\.gz/);
  assert.match(publish.run, /ContainerArchiveOutputPath=.*worker-image\.tar\.gz/);
  assert.doesNotMatch(publishJob.steps.map((step) => step.uses ?? '').join('\n'), /azure\/login/i);
  assert.doesNotMatch(source, /id-token:\s*write/);
  assert.match(terraformJob.steps[1].run, /fmt -check -recursive infra\/terraform/);
  assert.match(terraformJob.steps[1].run, /init -backend=false/);
  assert.match(terraformJob.steps[1].run, /validate/);
  assert.doesNotMatch(terraformJob.steps[1].run, /terraform .*apply/);
  for (const root of [
    'infra/terraform/bootstrap',
    'infra/terraform/environments/testing',
    'infra/terraform/environments/production',
    'infra/terraform/cloudflare/bootstrap',
    'infra/terraform/cloudflare/environments/testing',
    'infra/terraform/cloudflare/environments/production',
  ]) {
    assert.ok(terraformJob.steps[1].run.includes(root), `PR Terraform validation must include ${root}`);
  }
});

test('Azure infrastructure workflow gates a saved plan with a second environment approval before apply', () => {
  const { source, workflow } = WORKFLOWS['infra-deploy'];
  const dispatch = workflow.on.workflow_dispatch;
  const planJob = workflow.jobs.plan;
  const applyJob = workflow.jobs.apply;
  const planSteps = planJob.steps;
  const applySteps = applyJob.steps;
  const checkout = planSteps.find((step) => step.name === '📥 Checkout dispatched main commit');
  const applyCheckout = applySteps.find((step) => step.name === '📥 Checkout the exact planned commit');
  const terraformSetup = planSteps.find((step) => step.name === '⚙️ Setup Terraform');
  const configurationCheck = planSteps.find((step) => step.name === '🔎 Validate dispatch configuration');
  const oidc = planSteps.find((step) => step.name === '🔑 Request Azure OIDC token');
  const format = planSteps.find((step) => step.name === '🧹 Format Terraform (offline)');
  const createPlan = planSteps.find((step) => step.name === '📋 Create and display Terraform plan');
  const uploadPlan = planSteps.find((step) => step.name === '📤 Upload saved plan for the protected apply job');
  const applyOidc = applySteps.find((step) => step.name === '🔑 Request Azure OIDC token');
  const downloadPlan = applySteps.find((step) => step.name === '📦 Download the reviewed plan');
  const applyPlan = applySteps.find((step) => step.name === '🔒 Verify and apply the exact reviewed plan');

  assert.deepEqual(Object.keys(workflow.on), ['workflow_dispatch']);
  assert.deepEqual(dispatch.inputs.target_environment.options, ['testing', 'production']);
  assert.deepEqual(dispatch.inputs.operation.options, ['plan', 'apply']);
  assert.equal(dispatch.inputs.operation.default, 'plan');
  assert.equal(dispatch.inputs.create_applications.default, false);
  assert.equal(planJob.if, "github.ref == 'refs/heads/main'");
  assert.equal(planJob.environment.name, '${{ inputs.target_environment }}');
  assert.deepEqual(planJob.permissions, { contents: 'read', 'id-token': 'write' });
  assert.equal(checkout.with.ref, '${{ github.sha }}');
  assert.equal(checkout.with['persist-credentials'], false);
  assert.equal(terraformSetup.uses, 'hashicorp/setup-terraform@dfe3c3f87815947d99a8997f908cb6525fc44e9e');
  assert.equal(planJob.env.TF_VAR_create_applications, '${{ inputs.create_applications }}');
  assert.equal(planJob.env.TF_VAR_allow_split_mode, "${{ vars.AZURE_ALLOW_SPLIT_MODE || 'false' }}");
  assert.equal(planJob.env.TF_VAR_api_trusted_proxy_networks, "${{ vars.AZURE_API_TRUSTED_PROXY_NETWORKS_JSON || '[]' }}");
  assert.ok(configurationCheck.run.includes('AZURE_STATE_STORAGE_ACCOUNT_NAME'));
  assert.ok(format.run.includes('terraform fmt -check -recursive infra/terraform'));
  assert.ok(planSteps.indexOf(format) < planSteps.indexOf(oidc));
  assert.ok(createPlan.run.includes('-backend-config=use_oidc=true'));
  assert.ok(createPlan.run.includes('testing.tfstate'));
  assert.ok(createPlan.run.includes('production.tfstate'));
  assert.match(createPlan.run, /terraform .*state list/);
  assert.match(createPlan.run, /No state file was found!/);
  assert.match(createPlan.run, /azurerm_container_app\.api\[0\]/);
  assert.match(createPlan.run, /export TF_VAR_create_applications=true/);
  assert.match(createPlan.run, /plan -input=false -lock-timeout=5m -out="\$PLAN_PATH"/);
  assert.match(createPlan.run, /terraform .* show -no-color "\$PLAN_PATH"/);
  assert.doesNotMatch(createPlan.run, /terraform .*apply/);
  assert.equal(uploadPlan.if, "inputs.operation == 'apply'");
  assert.match(uploadPlan.uses, /^actions\/upload-artifact@/);
  assert.equal(uploadPlan.with['retention-days'], 1);

  assert.equal(applyJob.if, "github.ref == 'refs/heads/main' && inputs.operation == 'apply'");
  assert.equal(applyJob.needs, 'plan');
  assert.equal(applyJob.environment.name, '${{ inputs.target_environment }}');
  assert.deepEqual(applyJob.permissions, { contents: 'read', 'id-token': 'write' });
  assert.equal(applyCheckout.with.ref, '${{ github.sha }}');
  assert.equal(applyCheckout.with['persist-credentials'], false);
  assert.ok(applySteps.indexOf(applyOidc) < applySteps.indexOf(downloadPlan));
  assert.equal(downloadPlan.with.name, '${{ needs.plan.outputs.artifact-name }}');
  assert.match(applyPlan.run, /sha256sum "\$PLAN_PATH"/);
  assert.match(applyPlan.run, /"\$ACTUAL_PLAN_SHA256" != "\$EXPECTED_PLAN_SHA256"/);
  assert.match(applyPlan.run, /terraform .*init -input=false/);
  assert.match(applyPlan.run, /terraform .*apply -input=false -auto-approve -lock-timeout=5m "\$PLAN_PATH"/);
  assert.doesNotMatch(applyPlan.run, /terraform .*plan/);
  assert.equal(planJob.environment.name, applyJob.environment.name);
  assert.equal(planJob.env.ARM_USE_OIDC, 'true');
  assert.equal(applyJob.env.ARM_USE_OIDC, 'true');
  assert.doesNotMatch(source, /azure\/login/i);
  assert.equal(workflow.permissions.contents, 'read');
});

test('Cloudflare readiness satisfies the assertion boundary and allows only the internal host', () => {
  assert.ok(CLOUDFLARE_WORKER_SOURCE.includes('const CONTAINER_HOSTNAME = "container";'));
  assert.ok(CLOUDFLARE_WORKER_SOURCE.includes('containerUrl.host = CONTAINER_HOSTNAME;'));
  assert.ok(CLOUDFLARE_WORKER_SOURCE.includes('`http://${CONTAINER_HOSTNAME}${API_READY_PATH}`'));
  assert.ok(
    CLOUDFLARE_WORKER_SOURCE.includes(
      '"AllowedHosts"] = `${getApiHostname(env.API_HOSTNAME)};${CONTAINER_HOSTNAME}`;',
    ),
  );
  for (const header of [
    '"X-RefTest-Client-IP": "127.0.0.1"',
    '"X-RefTest-Forwarded-Proto": "https"',
    '"X-RefTest-Forwarded-Host": getApiHostname(this.env.API_HOSTNAME)',
    '"X-RefTest-Proxy-Assertion": getProxyAssertionSecret(',
  ]) {
    assert.ok(CLOUDFLARE_WORKER_SOURCE.includes(header), `Readiness must send ${header}`);
  }
  assert.match(CLOUDFLARE_WORKER_SOURCE, /forwarded\.headers\.set\(\s*"X-RefTest-Proxy-Assertion"/);
  assert.doesNotMatch(CLOUDFLARE_WORKER_SOURCE, /"AllowedHosts"\]\s*=\s*["'`]\*["'`]/);
});

test('ACA logs use Azure Monitor diagnostics without workspace shared-key ingestion', () => {
  const environmentBlock = ACA_TERRAFORM_SOURCE.match(
    /resource "azurerm_container_app_environment" "environment" \{([\s\S]*?)\n\}/,
  )?.[1];
  assert.ok(environmentBlock);
  assert.match(environmentBlock, /logs_destination\s+=\s+"azure-monitor"/);
  assert.doesNotMatch(environmentBlock, /log_analytics_workspace_id/);
  assert.match(ACA_TERRAFORM_SOURCE, /disableLocalAuth\s+=\s+true/);
  assert.doesNotMatch(ACA_TERRAFORM_SOURCE, /sharedKey\s*=/);
  assert.match(
    ACA_TERRAFORM_SOURCE,
    /resource "azurerm_monitor_diagnostic_setting" "container_app_environment"/,
  );
  assert.match(ACA_TERRAFORM_SOURCE, /log_analytics_destination_type\s+=\s+"Dedicated"/);
  assert.match(ACA_TERRAFORM_SOURCE, /category\s+=\s+"ContainerAppConsoleLogs"/);
  assert.match(ACA_TERRAFORM_SOURCE, /category\s+=\s+"ContainerAppSystemLogs"/);
});

test('CodeQL uses the release validation .NET SDK', () => {
  const setupDotnet = WORKFLOWS.codeql.workflow.jobs.analyze.steps.find(
    (step) => step.uses?.startsWith('actions/setup-dotnet@'),
  );
  assert.equal(setupDotnet.with['dotnet-version'], '10.0.303');
});

test('all workflow actions stay pinned to full commit SHAs with version comments', () => {
  for (const { source } of Object.values(WORKFLOWS)) {
    const actionLines = source
      .split('\n')
      .filter((line) => /^\s+uses:/.test(line) && !line.includes('./.github/workflows/'));
    assert.ok(actionLines.length > 0);
    for (const line of actionLines.map((line) => line.trimEnd())) {
      assert.match(line, /@[0-9a-f]{40}\s+#\s+v[\w.-]+$/);
    }
  }
});

test('badge preparation pushes a badge-only child before tagging the validated candidate', (context) => {
  const fixture = createRepository();
  context.after(() => rmSync(fixture.directory, { recursive: true, force: true }));
  const outputPath = join(fixture.directory, 'github-output.txt');
  writeFileSync(outputPath, '');

  const result = runBadgePrepare({ ...fixture, outputPath });
  assert.equal(result.status, 0, result.stderr || result.stdout);
  const branchSha = remoteBranchSha(fixture.remote);
  assert.equal(git(fixture.repo, ['rev-parse', 'HEAD']), fixture.validatedSha);
  assert.equal(branchSha, readFileSync(outputPath, 'utf8').match(/^release_branch_sha=(\w+)$/m)?.[1]);
  assert.equal(remoteGit(fixture.remote, ['rev-list', '--parents', '-n', '1', branchSha]).split(' ')[1], fixture.validatedSha);
  assert.deepEqual(
    remoteGit(fixture.remote, ['diff-tree', '--no-commit-id', '--name-only', '-r', branchSha]).split('\n'),
    ['badges/pre-release.png'],
  );

  git(fixture.repo, ['checkout', '--detach', '--force', branchSha]);
  const retry = runBadgePrepare({ ...fixture, outputPath });
  assert.equal(retry.status, 0, retry.stderr || retry.stdout);
  assert.equal(git(fixture.repo, ['rev-parse', 'HEAD']), fixture.validatedSha);
  assert.equal(remoteBranchSha(fixture.remote), branchSha);

  const tag = 'v3.2.1-alpha.1';
  git(fixture.repo, ['tag', tag, fixture.validatedSha]);
  git(fixture.repo, ['push', 'origin', `refs/tags/${tag}`]);
  assert.equal(remoteGit(fixture.remote, ['rev-parse', `refs/tags/${tag}^{commit}`]), fixture.validatedSha);
});

test('a rejected badge push aborts prepare and leaves the candidate untagged', (context) => {
  const fixture = createRepository();
  context.after(() => rmSync(fixture.directory, { recursive: true, force: true }));
  const hook = join(fixture.remote, 'hooks/pre-receive');
  writeFileSync(hook, '#!/bin/sh\nwhile read old new ref; do\n  if [ "$ref" = "refs/heads/main" ]; then exit 1; fi\ndone\nexit 0\n');
  chmodSync(hook, 0o755);
  const outputPath = join(fixture.directory, 'github-output.txt');
  writeFileSync(outputPath, '');

  const result = runBadgePrepare({ ...fixture, outputPath });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Badge branch push failed/);
  assert.equal(remoteBranchSha(fixture.remote), fixture.validatedSha);
  assert.equal(git(fixture.repo, ['tag', '--list']), '');
  assert.equal(remoteGit(fixture.remote, ['tag', '--list']), '');
  assert.equal(readFileSync(outputPath, 'utf8'), '');
});

test('badge generation failure prevents branch update and leaves the candidate untagged', (context) => {
  const fixture = createRepository();
  context.after(() => rmSync(fixture.directory, { recursive: true, force: true }));
  renameSync(join(fixture.repo, 'badges'), join(fixture.repo, 'badges-original'));
  writeFileSync(join(fixture.repo, 'badges'), 'not a directory');
  const outputPath = join(fixture.directory, 'github-output.txt');
  writeFileSync(outputPath, '');

  const result = runBadgePrepare({ ...fixture, outputPath });
  assert.notEqual(result.status, 0);
  assert.equal(remoteBranchSha(fixture.remote), fixture.validatedSha);
  assert.equal(git(fixture.repo, ['tag', '--list']), '');
  assert.equal(remoteGit(fixture.remote, ['tag', '--list']), '');
  assert.equal(readFileSync(outputPath, 'utf8'), '');
});

test('semantic-release runs badge publication before GitHub release publishing', () => {
  const execPluginIndex = releaseConfig.plugins.findIndex(
    (plugin) => Array.isArray(plugin) && plugin[0] === '@semantic-release/exec',
  );
  const githubPluginIndex = releaseConfig.plugins.indexOf('@semantic-release/github');
  assert.notEqual(execPluginIndex, -1);
  assert.ok(execPluginIndex < githubPluginIndex);
  assert.equal(
    releaseConfig.plugins[execPluginIndex][1].prepareCmd,
    'node scripts/generate-badges.mjs ${nextRelease.version} --publish',
  );

  const badgeScript = readFileSync(SCRIPT, 'utf8');
  assert.match(badgeScript, /if \(option === '--publish'\) \{\s+publishBadgeCommit\(\{/);
  assert.match(badgeScript, /checkout', '--detach', '--force'/);
  for (const name of ['beta-release', 'stable-release']) {
    const workflow = WORKFLOWS[name].workflow;
    const releaseStep = workflow.jobs.release.steps.find((step) => step.id === 'semantic-release');
    assert.match(releaseStep.run, /set -euo pipefail/);
    assert.equal(releaseStep.env.BADGE_BRANCH, name === 'beta-release' ? 'main' : 'release');
    assert.doesNotMatch(releaseStep.run, /git (?:add|commit) badges/);
  }
});

test('CODEOWNERS and README match the active conditional semantic-release config', () => {
  const codeOwners = readFileSync(join(ROOT, '.github/CODEOWNERS'), 'utf8');
  const readme = readFileSync(join(ROOT, 'README.md'), 'utf8');
  const releaseConfig = readFileSync(join(ROOT, '.releaserc.mjs'), 'utf8');

  assert.match(codeOwners, /^\* @KristofGilis$/m);
  assert.match(codeOwners, /^\/\.releaserc\.mjs @KristofGilis$/m);
  assert.doesNotMatch(codeOwners, /^-\s+@KristofGilis$/m);
  assert.doesNotMatch(codeOwners, /^\/\.releaserc\.json\b/m);
  assert.match(
    readme,
    /Every push to `main` triggers the beta workflow, but semantic-release publishes a pre-release only when commit analysis finds a releasable change\./,
  );
  assert.match(releaseConfig, /'@semantic-release\/commit-analyzer'/);
  assert.match(releaseConfig, /releaseRules:/);
});
