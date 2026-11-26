table 50110 "Portal User"
{
    Caption = 'Portal User';
    DataClassification = CustomerContent;

    fields
    {
        field(1; Email; Text[100])
        {
            Caption = 'Email';
            NotBlank = true;
        }
        field(2; "First Name"; Text[50])
        {
            Caption = 'First Name';
        }
        field(3; "Last Name"; Text[50])
        {
            Caption = 'Last Name';
        }
        field(4; "External User Id"; Text[50])
        {
            Caption = 'External User Id';
        }
        field(5; "Created At"; DateTime)
        {
            Caption = 'Created At';
            Editable = false;
        }
    }

    keys
    {
        key(PK; Email)
        {
            Clustered = true;
        }
    }
}
