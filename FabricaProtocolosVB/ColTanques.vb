Public Class ColTanques
    Implements IEnumerable

    'variable local para contener colección
    Private mCol As New Collection

    Public Function Add(CodTanque As String, EsActivo As Boolean, Stock As String, VolumenAgua As String) As TanqueInventario
        'crear un nuevo objeto
        Dim objNewMember As TanqueInventario
        objNewMember = New TanqueInventario


        'establecer las propiedades que se transfieren al método
        objNewMember.CodTanque = CodTanque
        objNewMember.EsActivo = EsActivo
        objNewMember.Stock = Stock
        objNewMember.VolumenAgua = VolumenAgua

        mCol.Add(objNewMember)

        'devolver el objeto creado
        Add = objNewMember
        objNewMember = Nothing

    End Function

    Public Sub AddMedicion(CodTanque As String, EsActivo As Boolean)
        'crear un nuevo objeto
        Dim objNewMember As TanqueInventario
        objNewMember = New TanqueInventario

        'establecer las propiedades que se transfieren al método
        objNewMember.CodTanque = CodTanque
        objNewMember.EsActivo = EsActivo

        mCol.Add(objNewMember)

        objNewMember = Nothing
    End Sub


    Public ReadOnly Property Item(vntIndexKey As Object) As TanqueInventario
        Get
            Return mCol(vntIndexKey)
        End Get
    End Property

    Public ReadOnly Property Count() As Long
        Get
            Return mCol.Count
        End Get
    End Property

    Public Sub Remove(vntIndexKey As Object)
        mCol.Remove(vntIndexKey)
    End Sub

    Sub New()
        'crea la colección cuando se crea la clase
        mCol = New Collection
    End Sub

    Public Function GetEnumerator() As IEnumerator _
       Implements IEnumerable.GetEnumerator

        Return New BaseEnum(mCol)
    End Function
    Public ReadOnly Property Items() As Collection
        Get
            Return mCol
        End Get
    End Property
End Class
