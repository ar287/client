USE SessionManagementDB;
GO
UPDATE Users SET PasswordHash = '$2a$11$qB9x2ioNxVdg3IalGgR/6uRNkwmGNYLkG5z9GW4rTPJs1z/ZirgCK' WHERE Username = 'admin';
UPDATE Users SET PasswordHash = '$2a$11$EMKkbMWPxkBODODaP83muOJ0HNqkmzZEBddBP832j.w/Eva0tTmVO' WHERE Username = 'customer1';
GO
