CREATE DATABASE SessionManagementDB;
GO

USE SessionManagementDB;
GO

CREATE TABLE Users (
    UserId        INT IDENTITY(1,1) PRIMARY KEY,
    FullName      NVARCHAR(100)  NOT NULL,
    Username      NVARCHAR(50)   NOT NULL UNIQUE,
    PasswordHash  NVARCHAR(255)  NOT NULL,
    Role          NVARCHAR(20)   NOT NULL CHECK (Role IN ('Admin', 'Customer')),
    IsActive      BIT            NOT NULL DEFAULT 1,
    CreatedAt     DATETIME       NOT NULL DEFAULT GETDATE()
);
GO

CREATE TABLE Sessions (
    SessionId       INT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT            NOT NULL FOREIGN KEY REFERENCES Users(UserId),
    StartTime       DATETIME       NOT NULL DEFAULT GETDATE(),
    EndTime         DATETIME       NULL,
    AllocatedMinutes INT           NOT NULL,
    RemainingMinutes INT           NOT NULL,
    Status          NVARCHAR(20)   NOT NULL DEFAULT 'Active'
                    CHECK (Status IN ('Active', 'Completed', 'Terminated')),
    ImagePath       NVARCHAR(500)  NULL,
    ClientMachine   NVARCHAR(100)  NULL
);
GO

CREATE TABLE Billing (
    BillingId       INT IDENTITY(1,1) PRIMARY KEY,
    SessionId       INT             NOT NULL FOREIGN KEY REFERENCES Sessions(SessionId),
    UserId          INT             NOT NULL FOREIGN KEY REFERENCES Users(UserId),
    RatePerMinute   DECIMAL(10, 2)  NOT NULL DEFAULT 2.00,
    TotalMinutes    INT             NOT NULL DEFAULT 0,
    TotalAmount     DECIMAL(10, 2)  NOT NULL DEFAULT 0.00,
    IsPaid          BIT             NOT NULL DEFAULT 0,
    GeneratedAt     DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

CREATE TABLE Alerts (
    AlertId       INT IDENTITY(1,1) PRIMARY KEY,
    UserId        INT            NULL FOREIGN KEY REFERENCES Users(UserId),
    AlertType     NVARCHAR(50)   NOT NULL,
    Description   NVARCHAR(500)  NOT NULL,
    Severity      NVARCHAR(20)   NOT NULL DEFAULT 'Low'
                  CHECK (Severity IN ('Low', 'Medium', 'High')),
    IsRead        BIT            NOT NULL DEFAULT 0,
    CreatedAt     DATETIME       NOT NULL DEFAULT GETDATE()
);
GO

CREATE TABLE Logs (
    LogId         INT IDENTITY(1,1) PRIMARY KEY,
    UserId        INT            NULL FOREIGN KEY REFERENCES Users(UserId),
    SessionId     INT            NULL FOREIGN KEY REFERENCES Sessions(SessionId),
    EventType     NVARCHAR(50)   NOT NULL,
    Description   NVARCHAR(500)  NOT NULL,
    IPAddress     NVARCHAR(50)   NULL,
    CreatedAt     DATETIME       NOT NULL DEFAULT GETDATE()
);
GO

-- Admin account (password: Admin@123)
INSERT INTO Users (FullName, Username, PasswordHash, Role)
VALUES (
    'System Administrator',
    'admin',
    '$2a$11$Xm1y6Z9oP3kL2nQ8wR4tUeKvBdCfGhIjMnOpQrStUvWxYzAbCdEf',
    'Admin'
);

-- Customer account (password: Customer@123)
INSERT INTO Users (FullName, Username, PasswordHash, Role)
VALUES (
    'Test Customer',
    'customer1',
    '$2a$11$Ym2z7A0pQ4lM3oR9xS5uVfLwCeEgHiJkNoQrStUvWxYzBcDeFgHi',
    'Customer'
);
GO

SELECT 
    TABLE_NAME,
    TABLE_TYPE
FROM 
    INFORMATION_SCHEMA.TABLES
WHERE 
    TABLE_TYPE = 'BASE TABLE'
ORDER BY 
    TABLE_NAME;
GO

SELECT UserId, FullName, Username, Role, IsActive 
FROM Users;
GO

SELECT @@SERVERNAME AS ServerName;
GO
