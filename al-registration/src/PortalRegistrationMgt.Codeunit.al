codeunit 50110 "Portal Registration Mgt"
{
    /// Published as SOAP service "PortalRegistration" on the Web Services page.
    /// Input:  {"email":"..","firstName":"..","lastName":"..","externalUserId":".."}
    /// Output: {"success":true|false,"message":".."}
    procedure RegisterUser(requestJson: Text): Text
    var
        Req: JsonObject;
        PortalUser: Record "Portal User";
        Email: Text;
    begin
        if not Req.ReadFrom(requestJson) then
            exit(Result(false, 'Invalid JSON'));

        Email := LowerCase(DelChr(GetText(Req, 'email'), '<>', ' '));
        if (Email = '') or not TryValidateEmail(Email) then
            exit(Result(false, 'Invalid email'));
        if (GetText(Req, 'firstName') = '') or (GetText(Req, 'lastName') = '') then
            exit(Result(false, 'First and last name are required'));
        if PortalUser.Get(Email) then
            exit(Result(false, 'Email already registered'));

        PortalUser.Init();
        PortalUser.Email := CopyStr(Email, 1, MaxStrLen(PortalUser.Email));
        PortalUser."First Name" := CopyStr(GetText(Req, 'firstName'), 1, MaxStrLen(PortalUser."First Name"));
        PortalUser."Last Name" := CopyStr(GetText(Req, 'lastName'), 1, MaxStrLen(PortalUser."Last Name"));
        PortalUser."External User Id" := CopyStr(GetText(Req, 'externalUserId'), 1, MaxStrLen(PortalUser."External User Id"));
        PortalUser."Created At" := CurrentDateTime();
        PortalUser.Insert(true);

        exit(Result(true, 'Registered'));
    end;

    local procedure GetText(Obj: JsonObject; KeyName: Text): Text
    var
        Tok: JsonToken;
    begin
        if Obj.Get(KeyName, Tok) then
            if Tok.IsValue() then
                exit(Tok.AsValue().AsText());
    end;

    [TryFunction]
    local procedure TryValidateEmail(Email: Text)
    var
        MailMgt: Codeunit "Mail Management";
    begin
        MailMgt.CheckValidEmailAddress(Email);
    end;

    local procedure Result(Success: Boolean; Msg: Text): Text
    var
        Obj: JsonObject;
        Txt: Text;
    begin
        Obj.Add('success', Success);
        Obj.Add('message', Msg);
        Obj.WriteTo(Txt);
        exit(Txt);
    end;
}
