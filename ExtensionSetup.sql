-- ============================================================================
-- ExtensionSetup.sql
-- Non-destructive Database Schema Extensions for
-- AI-Powered Cyber Lab Monitoring, Operations Analytics & Security Intelligence
-- ============================================================================

USE SessionManagementDB;
GO

-- 1. ClientMachines Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ClientMachines')
BEGIN
    CREATE TABLE ClientMachines (
        MachineId        INT IDENTITY(1,1) PRIMARY KEY,
        ClientId         NVARCHAR(100)  NOT NULL UNIQUE, -- e.g. LAB-PC-01
        MachineName      NVARCHAR(100)  NOT NULL,
        IPAddress        NVARCHAR(50)   NULL,
        MacAddress       NVARCHAR(50)   NULL,
        OSVersion        NVARCHAR(100)  NULL,
        Status           NVARCHAR(20)   NOT NULL DEFAULT 'Offline' -- Online, Offline, InUse, Locked, UnderReview
                         CHECK (Status IN ('Online', 'Offline', 'InUse', 'Locked', 'UnderReview')),
        LastHeartbeatUtc DATETIME       NULL,
        CurrentUserId    INT            NULL FOREIGN KEY REFERENCES Users(UserId),
        CurrentSessionId INT            NULL FOREIGN KEY REFERENCES Sessions(SessionId),
        RegisteredAtUtc  DATETIME       NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_ClientMachines_ClientId ON ClientMachines(ClientId);
    CREATE INDEX IX_ClientMachines_Status ON ClientMachines(Status);
END
GO

-- 2. ActivityEvents Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ActivityEvents')
BEGIN
    CREATE TABLE ActivityEvents (
        EventId       BIGINT IDENTITY(1,1) PRIMARY KEY,
        EventType     NVARCHAR(50)   NOT NULL,
        EventName     NVARCHAR(100)  NOT NULL,
        Severity      NVARCHAR(20)   NOT NULL DEFAULT 'Info'
                      CHECK (Severity IN ('Info', 'Low', 'Medium', 'High', 'Critical')),
        TimestampUtc  DATETIME       NOT NULL DEFAULT GETUTCDATE(),
        ClientId      NVARCHAR(100)  NULL,
        UserId        INT            NULL FOREIGN KEY REFERENCES Users(UserId),
        SessionId     INT            NULL FOREIGN KEY REFERENCES Sessions(SessionId),
        Source        NVARCHAR(50)   NOT NULL DEFAULT 'Client',
        Message       NVARCHAR(1000) NOT NULL,
        MetadataJson  NVARCHAR(MAX)  NULL,
        CorrelationId NVARCHAR(64)   NOT NULL DEFAULT NEWID(),
        IPAddress     NVARCHAR(50)   NULL
    );

    CREATE INDEX IX_ActivityEvents_ClientId ON ActivityEvents(ClientId);
    CREATE INDEX IX_ActivityEvents_UserId ON ActivityEvents(UserId);
    CREATE INDEX IX_ActivityEvents_SessionId ON ActivityEvents(SessionId);
    CREATE INDEX IX_ActivityEvents_EventType ON ActivityEvents(EventType);
    CREATE INDEX IX_ActivityEvents_Severity ON ActivityEvents(Severity);
    CREATE INDEX IX_ActivityEvents_TimestampUtc ON ActivityEvents(TimestampUtc);
END
GO

-- 3. SecurityRules Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SecurityRules')
BEGIN
    CREATE TABLE SecurityRules (
        RuleId              INT IDENTITY(1,1) PRIMARY KEY,
        RuleName            NVARCHAR(150)  NOT NULL UNIQUE,
        Description         NVARCHAR(500)  NULL,
        Severity            NVARCHAR(20)   NOT NULL DEFAULT 'Medium'
                            CHECK (Severity IN ('Low', 'Medium', 'High', 'Critical')),
        Scope               NVARCHAR(50)   NOT NULL DEFAULT 'AllPCs' -- SamePC, SameUser, SameSession, SameIP, SelectedPCs, AllPCs
                            CHECK (Scope IN ('SamePC', 'SameUser', 'SameSession', 'SameIP', 'SelectedPCs', 'AllPCs')),
        TargetScopeValue    NVARCHAR(200)  NULL,
        IsActive            BIT            NOT NULL DEFAULT 1,
        IsDraft             BIT            NOT NULL DEFAULT 0,
        RequiresApproval    BIT            NOT NULL DEFAULT 0,
        CooldownMinutes     INT            NOT NULL DEFAULT 5,
        CreatedAtUtc        DATETIME       NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy           NVARCHAR(50)   NOT NULL DEFAULT 'System'
    );
END
GO

-- 4. RuleConditions Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RuleConditions')
BEGIN
    CREATE TABLE RuleConditions (
        ConditionId     INT IDENTITY(1,1) PRIMARY KEY,
        RuleId          INT            NOT NULL FOREIGN KEY REFERENCES SecurityRules(RuleId) ON DELETE CASCADE,
        MetricType      NVARCHAR(50)   NOT NULL, -- FailedLogin, UnauthorizedProcess, InvalidToken, OfflineUnexpected, ThresholdCount
        Operator        NVARCHAR(20)   NOT NULL, -- Equals, NotEquals, GreaterThan, GreaterThanOrEqual, LessThan, CountInWindow, Exists
        ThresholdValue  NVARCHAR(100)  NOT NULL,
        WindowMinutes   INT            NOT NULL DEFAULT 10
    );
END
GO

-- 5. RuleActions Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RuleActions')
BEGIN
    CREATE TABLE RuleActions (
        ActionId         INT IDENTITY(1,1) PRIMARY KEY,
        RuleId           INT            NOT NULL FOREIGN KEY REFERENCES SecurityRules(RuleId) ON DELETE CASCADE,
        ActionType       NVARCHAR(50)   NOT NULL, -- CreateAlert, NotifyAdmin, RequestAiAnalysis, BlockNewSession, MarkClientUnderReview, LockClient, PauseSession, TerminateSession, DisconnectNetwork, RestartClient, ShutdownClient
        RequiresApproval BIT            NOT NULL DEFAULT 0
    );
END
GO

-- 6. RuleExecutions Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RuleExecutions')
BEGIN
    CREATE TABLE RuleExecutions (
        ExecutionId     BIGINT IDENTITY(1,1) PRIMARY KEY,
        RuleId          INT            NOT NULL FOREIGN KEY REFERENCES SecurityRules(RuleId),
        ClientId        NVARCHAR(100)  NULL,
        UserId          INT            NULL FOREIGN KEY REFERENCES Users(UserId),
        SessionId       INT            NULL FOREIGN KEY REFERENCES Sessions(SessionId),
        TriggeredAtUtc  DATETIME       NOT NULL DEFAULT GETUTCDATE(),
        RiskScoreDelta  INT            NOT NULL DEFAULT 0,
        ActionTaken     NVARCHAR(100)  NOT NULL,
        Status          NVARCHAR(50)   NOT NULL DEFAULT 'Executed' -- Executed, PendingApproval, Skipped
    );

    CREATE INDEX IX_RuleExecutions_RuleId ON RuleExecutions(RuleId);
    CREATE INDEX IX_RuleExecutions_ClientId ON RuleExecutions(ClientId);
    CREATE INDEX IX_RuleExecutions_TriggeredAtUtc ON RuleExecutions(TriggeredAtUtc);
END
GO

-- 7. ClientCommands Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ClientCommands')
BEGIN
    CREATE TABLE ClientCommands (
        CommandId        BIGINT IDENTITY(1,1) PRIMARY KEY,
        TargetClientId   NVARCHAR(100)  NOT NULL,
        CommandType      NVARCHAR(50)   NOT NULL, -- LockClient, UnlockClient, TerminateSession, PauseSession, ResumeSession, RestartClient, ShutdownClient, MessageBanner
        ParametersJson   NVARCHAR(MAX)  NULL,
        CreatedBy        NVARCHAR(50)   NOT NULL,
        ApprovedBy       NVARCHAR(50)   NULL,
        Reason           NVARCHAR(500)  NOT NULL,
        RuleId           INT            NULL FOREIGN KEY REFERENCES SecurityRules(RuleId),
        CreatedAtUtc     DATETIME       NOT NULL DEFAULT GETUTCDATE(),
        ExpiresAtUtc     DATETIME       NOT NULL,
        Status           NVARCHAR(30)   NOT NULL DEFAULT 'Created'
                         CHECK (Status IN ('Created', 'PendingApproval', 'Approved', 'Rejected', 'Sent', 'Acknowledged', 'Executed', 'Failed', 'Expired')),
        ExecutionResult  NVARCHAR(MAX)  NULL
    );

    CREATE INDEX IX_ClientCommands_TargetClientId ON ClientCommands(TargetClientId);
    CREATE INDEX IX_ClientCommands_Status ON ClientCommands(Status);
END
GO

-- 8. RiskAssessments Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RiskAssessments')
BEGIN
    CREATE TABLE RiskAssessments (
        AssessmentId     BIGINT IDENTITY(1,1) PRIMARY KEY,
        ClientId         NVARCHAR(100)  NOT NULL,
        UserId           INT            NULL FOREIGN KEY REFERENCES Users(UserId),
        RiskScore        INT            NOT NULL DEFAULT 0,
        RiskLevel        NVARCHAR(20)   NOT NULL DEFAULT 'Low'
                         CHECK (RiskLevel IN ('Low', 'Medium', 'High', 'Critical')),
        ReasonsJson      NVARCHAR(MAX)  NULL,
        EvidenceEvents   NVARCHAR(MAX)  NULL,
        RecommendedAction NVARCHAR(500) NULL,
        EvaluatedBy      NVARCHAR(50)   NOT NULL DEFAULT 'RuleEngine', -- RuleEngine, OllamaAI, FallbackRule
        ConfidenceScore  DECIMAL(5,2)   NOT NULL DEFAULT 1.00,
        CreatedAtUtc     DATETIME       NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_RiskAssessments_ClientId ON RiskAssessments(ClientId);
    CREATE INDEX IX_RiskAssessments_RiskLevel ON RiskAssessments(RiskLevel);
END
GO

-- 9. DailyOperationalReports Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DailyOperationalReports')
BEGIN
    CREATE TABLE DailyOperationalReports (
        ReportId         INT IDENTITY(1,1) PRIMARY KEY,
        ReportDate       DATE           NOT NULL UNIQUE,
        ReportType       NVARCHAR(20)   NOT NULL DEFAULT 'Daily' CHECK (ReportType IN ('Daily', 'Weekly')),
        TotalRevenue     DECIMAL(10,2)  NOT NULL DEFAULT 0.00,
        TotalSessions    INT            NOT NULL DEFAULT 0,
        TotalUsageMinutes INT           NOT NULL DEFAULT 0,
        AvgSessionMinutes INT           NOT NULL DEFAULT 0,
        PeakHour         INT            NOT NULL DEFAULT 14,
        MostUsedPc       NVARCHAR(100)  NULL,
        MostProfitablePc NVARCHAR(100)  NULL,
        SecurityAlertCount INT          NOT NULL DEFAULT 0,
        HighRiskPcCount  INT            NOT NULL DEFAULT 0,
        SummaryJson      NVARCHAR(MAX)  NULL,
        AiNarrative      NVARCHAR(MAX)  NULL,
        GeneratedAtUtc   DATETIME       NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

-- 10. AdminAuditEvents Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AdminAuditEvents')
BEGIN
    CREATE TABLE AdminAuditEvents (
        AuditId          BIGINT IDENTITY(1,1) PRIMARY KEY,
        AdminUsername    NVARCHAR(50)   NOT NULL,
        ActionType       NVARCHAR(100)  NOT NULL,
        Target           NVARCHAR(200)  NOT NULL,
        DetailsJson      NVARCHAR(MAX)  NULL,
        IPAddress        NVARCHAR(50)   NULL,
        TimestampUtc     DATETIME       NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_AdminAuditEvents_AdminUsername ON AdminAuditEvents(AdminUsername);
    CREATE INDEX IX_AdminAuditEvents_TimestampUtc ON AdminAuditEvents(TimestampUtc);
END
GO
