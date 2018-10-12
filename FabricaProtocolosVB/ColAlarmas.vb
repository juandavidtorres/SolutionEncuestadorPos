Public Class ColAlarmas
    Implements IEnumerable

    'variable local para contener colección
    Private mCol As New Collection

    Public Function Add(IdAlarma As Integer, Descripcion As String, Valor As String) As Alarma
        'crear un nuevo objeto
        Dim objNewMember As Alarma
        objNewMember = New Alarma


        'establecer las propiedades que se transfieren al método
        objNewMember.Valor = Valor
        objNewMember.IdAlarma = IdAlarma
        objNewMember.Descripcion = Descripcion

        mCol.Add(objNewMember)

        'devolver el objeto creado
        Add = objNewMember
        objNewMember = Nothing


    End Function
    Public ReadOnly Property Item(vntIndexKey As Object) As Alarma
        Get
            Return mCol(vntIndexKey)
        End Get
    End Property

    Public ReadOnly Property Items() As Collection
        Get
            Return mCol
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

End Class
