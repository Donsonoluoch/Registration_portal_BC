permissionset 50110 "Portal Reg"
{
    Caption = 'Portal Registration';
    Assignable = true;

    Permissions =
        tabledata "Portal User" = RIMD,
        table "Portal User" = X,
        codeunit "Portal Registration Mgt" = X;
}
