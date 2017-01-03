using System;
using System.Collections.Specialized;
using System.Linq;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Data.Templates;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Resources;
using Sitecore.SecurityModel;
using Sitecore.Shell.Applications.Dialogs;
using Sitecore.Shell.Applications.Dialogs.ItemLister;
using Sitecore.Shell.Applications.Dialogs.SelectCreateItem;
using Sitecore.StringExtensions;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;

namespace Sitecore.Support.Shell.Applications.WebEdit.Dialogs
{
  public class PlaceholderSettingsForm : SelectCreateItemForm
  {
    private const string EditMode = "Edit";
    protected Listbox AllowedControls;
    protected ThemedImage CreateIcon;
    protected Border CreateOption;
    protected Border CreateSection;
    private Item currentSettingItem;
    protected Checkbox Editable;
    protected Button EditControls;
    protected Border EditOption;
    protected Edit EditPlaceholderKey;
    protected Border EditSection;
    protected Literal Information;
    protected Edit NewSettingsName;
    protected Edit NewSettingsPlaceholderKey;
    protected TreePicker Parent;
    protected DataContext ParentDataContext;
    protected Edit PlaceholderKey;
    protected Literal SectionHeader;
    protected Literal SelectedSettingName;
    protected Listbox SelectedSettingsAllowedControls;
    protected Checkbox SelectedSettingsEditable;
    protected Button SelectedSettingsEditControls;
    protected Border SelectOption;
    protected Scrollbox SelectSection;
    private Template templateForCreation;
    protected Border Warnings;

    protected override Control CreateOptionControl =>
      CreateOption;

    private Item CurrentSettingItem
    {
      get
      {
        if (currentSettingItem != null) return currentSettingItem;
        var str = ServerProperties["current_settings"] as string;

        if (!string.IsNullOrEmpty(str)) currentSettingItem = Database.GetItem(new ItemUri(str));
        return currentSettingItem;
      }
      set
      {
        Assert.IsNotNull(value, "value");
        currentSettingItem = value;
        ServerProperties["current_settings"] = value.Uri.ToString();
      }
    }

    protected override Control SelectOptionControl =>
      SelectOption;

    private Template TemplateForCreation
    {
      get
      {
        if (templateForCreation != null) return templateForCreation;
        var obj2 = ServerProperties["tmpl_creation"];

        if (obj2 == null) return templateForCreation;
        var templateId = ID.Parse(obj2);
        templateForCreation = TemplateManager.GetTemplate(templateId, Client.ContentDatabase);

        return templateForCreation;
      }
      set
      {
        Assert.IsNotNull(value, "value");
        templateForCreation = value;
        ServerProperties["tmpl_creation"] = value.ID.ToString();
      }
    }

    protected override void ChangeMode(string mode)
    {
      Assert.ArgumentNotNull(mode, "mode");
      base.ChangeMode(mode);
      if (!UIUtil.IsIE() && (mode == "Select")) SheerResponse.Eval("scForm.browser.initializeFixsizeElements();");
    }

    private Item CreateNewSettings()
    {
      string str2;
      var str = Parent.Value;
      Assert.IsNotNullOrEmpty(str, "itemPath");
      var item = Client.ContentDatabase.GetItem(str);

      if (item == null)
      {
        SheerResponse.Alert("Select an item.");
        return null;
      }

      var name = NewSettingsName.Value;

      if (!ValidateNewItemName(name, out str2))
      {
        SheerResponse.Alert(str2);
        return null;
      }

      if (NewSettingsPlaceholderKey.Value.Trim().Length == 0)
      {
        SheerResponse.Alert("Specify a placeholder name.");
        return null;
      }

      var item2 = item.Add(name, new TemplateID(TemplateForCreation.ID));

      if (item2 == null) return null;

      item2.Editing.BeginEdit();
      ((CheckboxField)item2.Fields["Editable"]).Checked = Editable.Checked;
      item2.Fields["Allowed Controls"].Value = GetAllowedContros(AllowedControls).ToString();
      item2.Editing.EndEdit();

      return item2;
    }

    protected void Editable_Click()
    {
      SetInputControlsState(Editable, AllowedControls, EditControls);
    }

    protected void EditAllowedControls(ClientPipelineArgs args)
    {
      Assert.ArgumentNotNull(args, "args");
      var str = args.Parameters["listboxControl"];

      Assert.IsNotNullOrEmpty(str, "listBoxId");
      var listbox = WebUtil.FindControl(Dialog, str) as Listbox;

      Assert.IsNotNull(listbox, "listbox");

      if (args.IsPostBack)
      {
        if ((args.Result == null) || (args.Result == "undefined")) return;
        var items = new ListString(args.Result);

        RenderList(listbox, items);
        SheerResponse.SetOuterHtml(listbox.ClientID, listbox);
      }
      else
      {
        var urlString = new UrlString(UIUtil.GetUri("control:TreeListExEditor"));
        var handle = new UrlHandle();
        var allowedContros = GetAllowedContros(listbox);
        var source = string.Empty;

        if (CurrentSettingItem != null)
        {
          var item = CurrentSettingItem.Template.GetField("Allowed Controls");
          if (item != null) source = item.Source;
        }
        else
        {
          var field = TemplateForCreation?.GetField("Allowed Controls");
          if (field != null) source = field.Source;
        }

        handle["value"] = allowedContros.ToString();
        handle["source"] = source;
        handle["language"] = DataContext.Language.ToString();
        handle.Add(urlString);

        SheerResponse.ShowModalDialog(urlString.ToString(), "1300px", "700px", string.Empty, true);
        args.WaitForPostBack();
      }
    }

    protected void EditAllowedControls_Click(string associatedListboxId)
    {
      Assert.ArgumentNotNull(associatedListboxId, "associatedListboxId");
      var parameters = new NameValueCollection { ["listboxControl"] = associatedListboxId };
      Context.ClientPage.Start(this, "EditAllowedControls", parameters);
    }

    private Item EditExistingSetting()
    {
      var settingItem = CurrentSettingItem;

      if (settingItem == null)
      {
        SheerResponse.Alert("Item \"{0}\" not found.");
        return null;
      }

      if (EditPlaceholderKey.Value.Trim().Length == 0)
      {
        SheerResponse.Alert("Specify a placeholder name.");
        return null;
      }

      settingItem.Editing.BeginEdit();
      ((CheckboxField)settingItem.Fields["Editable"]).Checked = SelectedSettingsEditable.Checked;
      settingItem.Fields["Allowed Controls"].Value = GetAllowedContros(SelectedSettingsAllowedControls).ToString();
      settingItem.Editing.EndEdit();

      return settingItem;
    }

    private ListString GetAllowedContros(Listbox control)
    {
      Assert.ArgumentNotNull(control, "control");
      return new ListString((from i in control.Items select ShortID.Decode(StringUtil.Left(i.ID, 0x20))).ToList());
    }

    private void InitEditingControls(Item settingsItem)
    {
      Assert.IsNotNull(settingsItem, "settingsItem");
      SelectedSettingName.Text = settingsItem.DisplayName;
      SelectedSettingName.ToolTip = settingsItem.Paths.FullPath;
      CheckboxField field = settingsItem.Fields["Editable"];

      if (field != null)
      {
        SelectedSettingsEditable.Checked = field.Checked;
        SetInputControlsState(SelectedSettingsEditable, SelectedSettingsAllowedControls, SelectedSettingsEditControls);
      }

      var str = settingsItem["Allowed Controls"];
      if (!string.IsNullOrEmpty(str)) RenderList(SelectedSettingsAllowedControls, new ListString(str));
    }

    protected override void OnLoad(EventArgs e)
    {
      Assert.ArgumentNotNull(e, "e");
      base.OnLoad(e);
      Parent.OnChanged += ParentChanged;

      if (Context.ClientPage.IsEvent) return;
      SelectOption.Click = "ChangeMode(\"Select\")";
      CreateOption.Click = "ChangeMode(\"Create\")";
      EditControls.Click = $"EditAllowedControls_Click(\"{AllowedControls.ID}\")";

      var options = SelectItemOptions.Parse<SelectPlaceholderSettingsOptions>();

      if (!string.IsNullOrEmpty(options.PlaceholderKey))
      {
        NewSettingsPlaceholderKey.Value = options.PlaceholderKey;
        PlaceholderKey.Value = options.PlaceholderKey;
        EditPlaceholderKey.Value = options.PlaceholderKey;
      }

      if (!options.IsPlaceholderKeyEditable)
      {
        NewSettingsPlaceholderKey.Disabled = true;
        PlaceholderKey.Disabled = true;
        EditPlaceholderKey.Disabled = true;
      }

      if (options.TemplateForCreating != null)
      {
        TemplateForCreation = options.TemplateForCreating;
        TemplateItem item = Client.ContentDatabase.GetItem(TemplateForCreation.ID);
        CheckboxField field = item?.StandardValues?.Fields["Editable"];

        if (field != null)
        {
          Editable.Checked = field.Checked;
          SetInputControlsState(Editable, AllowedControls, EditControls);
        }

        if (!string.IsNullOrEmpty(DataContext.Root))
        {
          ParentDataContext.Root = DataContext.Root;

          if (!string.IsNullOrEmpty(options.PlaceholderKey))
          {
            var parent = Client.ContentDatabase.GetItem(ParentDataContext.Root);
            NewSettingsName.Value = GetNewItemDefaultName(parent,
              StringUtil.GetLastPart(options.PlaceholderKey, '/', options.PlaceholderKey));
          }
        }
        ParentDataContext.DataViewName = DataContext.DataViewName;
      }
      else
      {
        CreateOption.Disabled = true;
        CreateOption.Class = "option-disabled";
        CreateOption.Click = "javascript:void(0);";
        CreateIcon.Src = Images.GetThemedImageSource(CreateIcon.Src, ImageDimension.id32x32, true);
        ParentDataContext.DataViewName = DataContext.DataViewName;
      }

      if (options.CurrentSettingsItem != null)
      {
        CurrentSettingItem = options.CurrentSettingsItem;
        EditOption.Visible = true;
        CurrentMode = "Edit";
        EditOption.Click = "ChangeMode(\"Edit\")";
        InitEditingControls(options.CurrentSettingsItem);
        SetControlsOnModeChange();
        SelectedSettingsEditControls.Click = $"EditAllowedControls_Click(\"{SelectedSettingsAllowedControls.ID}\")";
      }
      else
      {
        var folder = DataContext.GetFolder();
        SetControlsForSelection(folder);
      }
      ResgisterStartupScripts();
    }

    protected override void OnOK(object sender, EventArgs args)
    {
      Assert.ArgumentNotNull(sender, "sender");
      Assert.ArgumentNotNull(args, "args");
      var currentMode = CurrentMode;
      if (currentMode == null) return;

      Item selectionItem;

      if (currentMode != "Edit")
      {
        if (currentMode != "Select")
        {
          if (currentMode != "Create") return;

          selectionItem = CreateNewSettings();

          if (selectionItem == null) return;
          SheerResponse.SetDialogValue(selectionItem.ID + "|" + NewSettingsPlaceholderKey.Value);
          SheerResponse.CloseWindow();

          return;
        }
      }
      else
      {
        if (EditExistingSetting() == null) return;

        SheerResponse.SetDialogValue($"{CurrentSettingItem.ID}|{EditPlaceholderKey.Value}");
        SheerResponse.CloseWindow();

        return;
      }

      selectionItem = Treeview.GetSelectionItem();

      if (selectionItem == null) return;

      if (PlaceholderKey.Value.Trim().Length == 0)
      {
        SheerResponse.Alert("Specify a placeholder name.");
      }
      else
      {
        SheerResponse.SetDialogValue(selectionItem.ID + "|" + PlaceholderKey.Value);
        SheerResponse.CloseWindow();
      }
    }

    protected void ParentChanged(object sender, EventArgs args)
    {
      Assert.ArgumentNotNull(sender, "sender");
      Assert.ArgumentNotNull(args, "args");
      SetControlsForCreating();
    }

    private void RenderList(Listbox listbox, ListString items)
    {
      Assert.ArgumentNotNull(listbox, "listbox");
      Assert.ArgumentNotNull(items, "items");
      listbox.Controls.Clear();

      foreach (var str in items)
      {
        ID id;
        if (!ID.TryParse(str, out id)) continue;

        var child = new ListItem
        {
          ID = id.ToShortID() + listbox.ClientID
        };

        var item = Client.ContentDatabase.GetItem(id);

        if (item == null) continue;

        child.Value = id.ToString();
        child.Header = item.DisplayName;
        listbox.Controls.Add(child);
      }
    }

    private void ResgisterStartupScripts()
    {
      string clientID = null;
      if ((CurrentMode == "Edit") && !EditPlaceholderKey.Disabled) clientID = EditPlaceholderKey.ClientID;
      if ((CurrentMode == "Select") && !PlaceholderKey.Disabled) clientID = PlaceholderKey.ClientID;
      if (clientID != null) Context.ClientPage.ClientScript.RegisterStartupScript(Context.ClientPage.GetType(), "startScript", $"selectValue('{clientID}');", true);
    }

    protected void SelectedEditable_Click()
    {
      SetInputControlsState(SelectedSettingsEditable, SelectedSettingsAllowedControls, SelectedSettingsEditControls);
    }

    private void SetControlsForCreating()
    {
      string str2;
      var str = Parent.Value;
      Assert.IsNotNullOrEmpty(str, "itemPath");

      var parent = Client.ContentDatabase.GetItem(str);
      Warnings.Visible = false;

      if (!CanCreateItem(parent, out str2))
      {
        OK.Disabled = true;
        Information.Text = Translate.Text(str2);
        Warnings.Visible = true;
      }
      else
      {
        OK.Disabled = false;
      }
    }

    private void SetControlsForEditing()
    {
      var settingItem = CurrentSettingItem;
      Warnings.Visible = false;

      if (settingItem == null)
      {
        OK.Disabled = true;
      }
      else if (!settingItem.Access.CanWrite())
      {
        OK.Disabled = true;
        Information.Text = Translate.Text("You cannot edit this item because you do not have write access to it.");
        Warnings.Visible = true;
      }
      else
      {
        var str = settingItem["Placeholder Key"];
        if (!string.IsNullOrEmpty(str))
        {
          Information.Text = Translate.Text("The settings affect all placeholders with '{0}' key.").FormatWith($"<span title=\"{str}\">{StringUtil.Clip(str, 0x19, true)}</span>");
          Warnings.Visible = true;
        }
        OK.Disabled = false;
      }
    }

    private void SetControlsForSelection(Item item)
    {
      Warnings.Visible = false;
      if (!Policy.IsAllowed("Page Editor/Can Select Placeholder Settings"))
      {
        OK.Disabled = true;
        var str = Translate.Text("The action cannot be executed because of security restrictions.");
        Information.Text = str;
        Warnings.Visible = true;
      }
      else if (item != null)
      {
        if (!IsSelectable(item))
        {
          OK.Disabled = true;
          var str2 = StringUtil.Clip(item.DisplayName, 20, true);
          var str3 = Translate.Text("The '{0}' item is not a valid selection.").FormatWith(str2);
          Information.Text = str3;
          Warnings.Visible = true;
        }
        else
        {
          Information.Text = string.Empty;
          OK.Disabled = false;
        }
      }
    }

    protected override void SetControlsOnModeChange()
    {
      base.SetControlsOnModeChange();
      var currentMode = CurrentMode;

      if (currentMode == null) return;

      if (currentMode != "Edit")
      {
        if (currentMode != "Select")
        {
          if (currentMode != "Create") return;
          SelectSection.Visible = false;
          CreateSection.Visible = true;
          EditSection.Visible = false;

          if (EditOption.Visible) EditOption.Class = string.Empty;

          SetControlsForCreating();
          SectionHeader.Text = Translate.Text("Create a new placeholder settings item.");
          SheerResponse.Eval($"selectValue('{NewSettingsName.ClientID}')");
          return;
        }
      }
      else
      {
        EditSection.Visible = true;
        CreateSection.Visible = false;
        SelectSection.Visible = false;
        EditOption.Class = "selected";
        SelectOption.Class = string.Empty;

        if (!CreateOption.Disabled) CreateOption.Class = string.Empty;
        SetControlsForEditing();
        SectionHeader.Text = Translate.Text("Edit the selected placeholder settings item.");

        if (!EditPlaceholderKey.Disabled) SheerResponse.Eval($"selectValue('{EditPlaceholderKey.ClientID}')");
        return;
      }
      SelectSection.Visible = true;
      EditSection.Visible = false;
      CreateSection.Visible = false;

      if (EditOption.Visible) EditOption.Class = string.Empty;
      SetControlsForSelection(Treeview.GetSelectionItem());
      SectionHeader.Text = Translate.Text("Select an existing placeholder settings item.");

      if (!PlaceholderKey.Disabled) SheerResponse.Eval($"selectValue('{PlaceholderKey.ClientID}')");
    }

    private void SetInputControlsState(Checkbox checkbox, Listbox listbox, Button button)
    {
      Assert.ArgumentNotNull(checkbox, "checkbox");
      Assert.ArgumentNotNull(listbox, "listbox");
      Assert.ArgumentNotNull(button, "button");

      button.Disabled = !checkbox.Checked;
      listbox.Disabled = !checkbox.Checked;
    }

    protected void Treeview_Click()
    {
      SetControlsForSelection(Treeview.GetSelectionItem());
    }
  }
}