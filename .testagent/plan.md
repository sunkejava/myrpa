# Test plan

- BusinessResourcesTests.Resource_codes_are_normalized_and_parent_ids_are_preserved: normalized code and parent linkage.
- BusinessResourcesTests.Invalid_resource_names_and_missing_parents_are_rejected: invalid input.
- BusinessResourcesTests.Disabling_city_or_system_updates_the_resource_state: active state.
- PermissionServiceTests.Disabled_or_mismatched_resource_scope_denies_even_existing_grant: fail-closed permission decision.
- API login and anonymous route checks: GitHub Actions backend-build smoke job; admin route enforcement integration test and full workflow permission matrix still pending.

- WorkflowPermissionPreflightTests.Nested_step_with_additional_action_requires_both_execute_and_approve: nested branch + required action denied.
- WorkflowPermissionPreflightTests.Every_step_is_allowed_when_all_actions_are_granted: all declared actions granted.
- WorkflowPermissionPreflightTests.Invalid_step_permission_declarations_fail_closed: invalid action and malformed nested branch rejected without repository policy lookups.
