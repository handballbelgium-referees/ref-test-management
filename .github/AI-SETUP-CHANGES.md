# AI setup hardening

This update focuses on three controls:

1. Security-sensitive hooks fail closed instead of silently allowing operations on errors.
2. Write operations whose filesystem targets cannot be resolved are denied rather than
   treated as implicitly safe.
3. Implementers are required to have a machine-verifiable approval artifact identifying
   the approved plan/work package and scope.

Review the existing agent/skill instructions alongside `AI-APPROVAL-CONTRACT.md`.
