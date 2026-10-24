using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using ControlStyles;

internal static class TextBoxFocusChecks
{
    public static int Count;
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);Count++;}
    private static bool All(TextBox box)=>box.SelectionStart==0&&box.SelectionEnd==(box.Text?.Length??0);
    private static void Pump()=>Dispatcher.UIThread.RunJobs();
    public static void UI()
    {
        TextBoxFocusSelection.Enable();TextBoxFocusSelection.Enable();
        TextBox[] boxes=[new(){Text="Numero tessera"},new UiTextBox{Text="MARIO ROSSI"},new UiIntegerTextBox{Text="123"},new UiEuroTextBox{Text="12,50"},
            new(){Text="Prima riga\nSeconda riga",AcceptsReturn=true},new(){Text="Sola lettura",IsReadOnly=true},new(){Text="Segreto",PasswordChar='*'},new(){Text=""}];
        var other=new Button{Content="Altro controllo"};var panel=new StackPanel{Spacing=6};panel.Children.Add(other);
        foreach(var box in boxes){box.Height=40;box.Width=300;panel.Children.Add(box);}
        var window=new Window{Width=500,Height=600,Content=panel};window.Show();Pump();
        foreach(var box in boxes){other.Focus();Pump();box.Focus();Pump();Check(All(box),"Focus seleziona tutto: "+box.GetType().Name);}
        boxes[0].Focus();Pump();window.KeyPress(Key.Tab,RawInputModifiers.None,PhysicalKey.Tab,null);Pump();
        Check(boxes[1].IsFocused&&All(boxes[1]),"Tab seleziona il testo nel nuovo campo");
        window.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,null);Pump();
        Check(boxes[2].IsFocused&&All(boxes[2]),"Invio di UiTextBox mantiene selezione completa nel campo successivo");
        other.Focus();Pump();
        var at=boxes[0].TranslatePoint(new Point(30,20),window)!.Value;
        window.MouseDown(at,MouseButton.Left,RawInputModifiers.None);window.MouseUp(at,MouseButton.Left,RawInputModifiers.None);Pump();
        Check(boxes[0].IsFocused&&All(boxes[0]),"Primo clic seleziona tutto dopo il posizionamento del cursore");
        at=boxes[0].TranslatePoint(new Point(100,20),window)!.Value;
        window.MouseDown(at,MouseButton.Left,RawInputModifiers.None);window.MouseUp(at,MouseButton.Left,RawInputModifiers.None);Pump();
        Check(boxes[0].SelectionStart==boxes[0].SelectionEnd,"Un clic sul campo già attivo permette di posizionare il cursore");
        other.Focus();Pump();boxes[1].Focus();boxes[1].CaretIndex=2;boxes[1].SelectionStart=2;boxes[1].SelectionEnd=2;other.Focus();Pump();
        Check(boxes[1].SelectionStart==2&&boxes[1].SelectionEnd==2,"Il callback ignora un campo che ha già perso il focus");
        boxes[1].Focus();Pump();window.KeyTextInput("X");Pump();Check(boxes[1].Text=="X","Scrivere dopo il focus sostituisce il testo selezionato");
        var integer=(UiIntegerTextBox)boxes[2];
        integer.Focus();Pump();integer.Text="";other.Focus();Pump();
        Check(integer.Text=="0","Intero vuoto diventa zero quando perde il focus");
        integer.Focus();Pump();integer.Text=null;other.Focus();Pump();
        Check(integer.Text=="0","Intero null diventa zero quando perde il focus");
        integer.Focus();Pump();integer.Text="   ";other.Focus();Pump();
        Check(integer.Text=="0","Spazi senza cifre vengono normalizzati a zero");
        integer.Focus();Pump();integer.Text="42";other.Focus();Pump();
        Check(integer.Text=="42","Valore intero compilato conservato all'uscita");
        integer.Focus();Pump();integer.Text="";window.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,null);Pump();
        Check(integer.Text=="0"&&boxes[3].IsFocused,"Invio normalizza il campo vuoto e passa al successivo");
        boxes[0].Focus();Pump();boxes[0].Text="";other.Focus();Pump();
        Check(boxes[0].Text=="","Le TextBox di testo mantengono il campo vuoto");
        window.Close();Pump();
    }
}
