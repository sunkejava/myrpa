using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRPA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "access_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SystemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FunctionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Resource = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Result = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "business_functions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SystemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AllowNaturalLanguage = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowBatch = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowExport = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_functions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "business_systems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_systems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "execution_nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    NodeKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    OsPlatform = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Architecture = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    NodePoolId = table.Column<Guid>(type: "TEXT", nullable: true),
                    NetworkZone = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    AgentVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_nodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "executions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DispatchKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    NodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WorkerSlotId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_executions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HumanInterventions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SecureEntry = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HumanInterventions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "node_pools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_node_pools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NodeLeases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkerSlotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Released = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeLeases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ExecutionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Released = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "role_access_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SystemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FunctionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_access_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MaxRetries = table.Column<int>(type: "INTEGER", nullable: false),
                    SubjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "worker_slots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SlotName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worker_slots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BusinessFunctionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "node_capabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                    ExecutionNodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_node_capabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_node_capabilities_execution_nodes_ExecutionNodeId",
                        column: x => x.ExecutionNodeId,
                        principalTable: "execution_nodes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "task_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    InputJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RpaTaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_items_tasks_RpaTaskId",
                        column: x => x.RpaTaskId,
                        principalTable: "tasks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    UserAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.UserAccountId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false),
                    Published = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_versions_workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_steps_workflow_versions_WorkflowVersionId",
                        column: x => x.WorkflowVersionId,
                        principalTable: "workflow_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_policies_SubjectId_CityId_SystemId_FunctionId_Action",
                table: "access_policies",
                columns: new[] { "SubjectId", "CityId", "SystemId", "FunctionId", "Action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_Actor_Resource",
                table: "audit_entries",
                columns: new[] { "Actor", "Resource" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_CreatedAt",
                table: "audit_entries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_business_functions_SystemId_Code",
                table: "business_functions",
                columns: new[] { "SystemId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_systems_CityId_Code",
                table: "business_systems",
                columns: new[] { "CityId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cities_Code",
                table: "cities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_nodes_AgentKey",
                table: "execution_nodes",
                column: "AgentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_nodes_LastHeartbeatAt",
                table: "execution_nodes",
                column: "LastHeartbeatAt");

            migrationBuilder.CreateIndex(
                name: "IX_execution_nodes_NodePoolId",
                table: "execution_nodes",
                column: "NodePoolId");

            migrationBuilder.CreateIndex(
                name: "IX_execution_nodes_Status",
                table: "execution_nodes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_executions_DispatchKey",
                table: "executions",
                column: "DispatchKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_executions_NodeId",
                table: "executions",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_executions_Status",
                table: "executions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_executions_TaskItemId",
                table: "executions",
                column: "TaskItemId");

            migrationBuilder.CreateIndex(
                name: "IX_HumanInterventions_ExecutionId_Status",
                table: "HumanInterventions",
                columns: new[] { "ExecutionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_node_capabilities_Code",
                table: "node_capabilities",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_node_capabilities_ExecutionNodeId",
                table: "node_capabilities",
                column: "ExecutionNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_node_capabilities_NodeId_Code",
                table: "node_capabilities",
                columns: new[] { "NodeId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_node_pools_Name",
                table: "node_pools",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NodeLeases_ExecutionId",
                table: "NodeLeases",
                column: "ExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_NodeLeases_WorkerSlotId_Released_ExpiresAt",
                table: "NodeLeases",
                columns: new[] { "WorkerSlotId", "Released", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocks_ExecutionId",
                table: "ResourceLocks",
                column: "ExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocks_ResourceType_ResourceId_ExpiresAt",
                table: "ResourceLocks",
                columns: new[] { "ResourceType", "ResourceId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocks_ResourceType_ResourceId_Released",
                table: "ResourceLocks",
                columns: new[] { "ResourceType", "ResourceId", "Released" },
                unique: true,
                filter: "Released = 0");

            migrationBuilder.CreateIndex(
                name: "IX_role_access_policies_RoleId",
                table: "role_access_policies",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_role_access_policies_RoleId_CityId_SystemId_FunctionId_Action",
                table: "role_access_policies",
                columns: new[] { "RoleId", "CityId", "SystemId", "FunctionId", "Action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_Name",
                table: "roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_items_RpaTaskId",
                table: "task_items",
                column: "RpaTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_task_items_Status",
                table: "task_items",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_task_items_TaskId_Sequence",
                table: "task_items",
                columns: new[] { "TaskId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tasks_Status",
                table: "tasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_SubjectId",
                table: "tasks",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_UserName",
                table: "user_accounts",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_RoleId",
                table: "user_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_worker_slots_ExecutionId",
                table: "worker_slots",
                column: "ExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_worker_slots_LeaseExpiresAt",
                table: "worker_slots",
                column: "LeaseExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_worker_slots_NodeId_SlotName",
                table: "worker_slots",
                columns: new[] { "NodeId", "SlotName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_steps_WorkflowVersionId_Order",
                table: "workflow_steps",
                columns: new[] { "WorkflowVersionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_versions_WorkflowId_Version",
                table: "workflow_versions",
                columns: new[] { "WorkflowId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflows_BusinessFunctionId",
                table: "workflows",
                column: "BusinessFunctionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_policies");

            migrationBuilder.DropTable(
                name: "audit_entries");

            migrationBuilder.DropTable(
                name: "business_functions");

            migrationBuilder.DropTable(
                name: "business_systems");

            migrationBuilder.DropTable(
                name: "cities");

            migrationBuilder.DropTable(
                name: "executions");

            migrationBuilder.DropTable(
                name: "HumanInterventions");

            migrationBuilder.DropTable(
                name: "node_capabilities");

            migrationBuilder.DropTable(
                name: "node_pools");

            migrationBuilder.DropTable(
                name: "NodeLeases");

            migrationBuilder.DropTable(
                name: "ResourceLocks");

            migrationBuilder.DropTable(
                name: "role_access_policies");

            migrationBuilder.DropTable(
                name: "task_items");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "worker_slots");

            migrationBuilder.DropTable(
                name: "workflow_steps");

            migrationBuilder.DropTable(
                name: "execution_nodes");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "user_accounts");

            migrationBuilder.DropTable(
                name: "workflow_versions");

            migrationBuilder.DropTable(
                name: "workflows");
        }
    }
}
