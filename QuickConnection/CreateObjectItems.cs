﻿using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuickConnection;

internal class CreateObjectItems()
{
    public SortedList<Guid, CreateObjectItem[]> InputItems { get; set; } = [];
    public SortedList<Guid, CreateObjectItem[]> OutputItems { get; set; } = [];
    public CreateObjectItem[] ListItems { get; set; } = [];
    public CreateObjectItem[] TreeItems { get; set; } = [];

    public CreateObjectItems(CreateObjectItemsSave items) : this()
    {
        InputItems = [];
        foreach (var inputPair in items.InputItems)
        {
            CreateObjectItem[] inputPairSave = new CreateObjectItem[inputPair.Value.Length];
            for (int i = 0; i < inputPair.Value.Length; i++)
            {
                inputPairSave[i] = new CreateObjectItem(inputPair.Value[i], true);
            }
            InputItems[new Guid(inputPair.Key)] = inputPairSave;
        }

        OutputItems = [];
        foreach (var outputPair in items.OutputItems)
        {
            CreateObjectItem[] outputPairSave = new CreateObjectItem[outputPair.Value.Length];
            for (int i = 0; i < outputPair.Value.Length; i++)
            {
                outputPairSave[i] = new CreateObjectItem(outputPair.Value[i], false);
            }
            OutputItems[new Guid(outputPair.Key)] = outputPairSave;
        }

        if (items.ListItems != null)
        {
            CreateObjectItem[] list = new CreateObjectItem[items.ListItems.Length];
            for (int i = 0; i < items.ListItems.Length; i++)
            {
                list[i] = new CreateObjectItem(items.ListItems[i], false);
            }
            ListItems = list;
        }

        if (items.TreeItems != null)
        {
            CreateObjectItem[] tree = new CreateObjectItem[items.TreeItems.Length];
            for (int i = 0; i < items.TreeItems.Length; i++)
            {
                tree[i] = new CreateObjectItem(items.TreeItems[i], false);
            }
            TreeItems = tree;
        }
    }

    public void CreateDefaultStyle(bool isCoreOnly, Guid[] ids = null)
    {
        SortedDictionary<Guid, List<CreateObjectItem>> inputItems = [], outputItems = [];
        SortedDictionary<Guid, Guid> inputRemap = [], outputRemap = [];
        List<CreateObjectItem> listItems = [], treeItems  = [];
        int createCount = 0;

        //Add every object to my dictionary.
        foreach (var proxy in Instances.ComponentServer.ObjectProxies)
        {
            //Check for if we should skip this object proxy.
            if (proxy.Kind != GH_ObjectType.CompiledObject) continue;
            if (proxy.Obsolete) continue;
            //if (proxy.Exposure.HasFlag(GH_Exposure.hidden)) continue;

            if (isCoreOnly)
            {
                bool isCore = false;
                foreach (var assembly in Instances.ComponentServer.Libraries)
                {
                    if (proxy.LibraryGuid == assembly.Id)
                    {
                        isCore = assembly.IsCoreLibrary;
                        break;
                    }
                }
                if (!isCore) continue;
            }
            else if(ids != null && ids.Length > 0)
            {
                if (!ids.Contains(proxy.LibraryGuid)) continue;
            }

            IGH_DocumentObject obj = proxy.CreateInstance();
            createCount++;

            //Case Component
            if (obj is IGH_Component component)
            {
                //Add for inputs.
                for (int i = 0; i < component.Params.Input.Count; i++)
                {
                    IGH_Param param = component.Params.Input[i];

                    //Check for if param's type is valid.
                    if (!IsParamGeneral(param.GetType(), out Type dataType)) continue;

                    inputRemap[param.ComponentGuid] = dataType.GUID;

                    //Find items set before.
                    if (!outputItems.TryGetValue(dataType.GUID, out List<CreateObjectItem> storedItems))
                    {
                        storedItems = [];
                    }

                    //Create the add item.
                    CreateObjectItem addItem = new CreateObjectItem(obj.ComponentGuid, (ushort)i, "", false);

                    //Check for tree or list case.
                    if (dataType == typeof(IGH_Goo))
                    {
                        if(param.Access == GH_ParamAccess.tree)
                        {
                            //Check for if already contained.
                            bool quitTree = false;
                            foreach (CreateObjectItem item in treeItems)
                            {
                                if (item.ObjectGuid == obj.ComponentGuid)
                                {
                                    quitTree = true;
                                    break;
                                }
                            }
                            if (!quitTree)
                            {
                                //Add to and skip.
                                treeItems.Add(addItem);
                            }
                            continue;
                        }
                        else if(param.Access == GH_ParamAccess.list)
                        {
                            //Check for if already contained.
                            bool quitList = false;
                            foreach (CreateObjectItem item in listItems)
                            {
                                if (item.ObjectGuid == obj.ComponentGuid)
                                {
                                    quitList = true;
                                    break;
                                }
                            }
                            if (!quitList)
                            {
                                //Add to and skip.
                                listItems.Add(addItem);
                            }
                            continue;
                        }
                    }

                    //Check for if already contained.
                    bool quit = false;
                    foreach (CreateObjectItem item in storedItems)
                    {
                        if (item.ObjectGuid == obj.ComponentGuid)
                        {
                            quit = true;
                            break;
                        }
                    }
                    if (quit) continue;

                    //Add to and save.
                    storedItems.Add(addItem);
                    outputItems[dataType.GUID] = storedItems;
                }

                //Add for outputs.
                for (int i = 0; i < component.Params.Output.Count; i++)
                {
                    IGH_Param param = component.Params.Output[i];

                    //Check for if param's type is valid.
                    if (!IsParamGeneral(param.GetType(), out Type dataType)) continue;

                    outputRemap[param.ComponentGuid] = dataType.GUID;

                    //Find items set before.
                    if (!inputItems.TryGetValue(dataType.GUID, out List<CreateObjectItem> storedItems))
                    {
                        storedItems = [];
                    }

                    //Check for if already contained.
                    bool quit = false;
                    foreach (CreateObjectItem item in storedItems)
                    {
                        if (item.ObjectGuid == obj.ComponentGuid)
                        {
                            quit = true;
                            break;
                        }
                    }
                    if (quit) continue;

                    //Add to and save.
                    storedItems.Add(new CreateObjectItem(obj.ComponentGuid, (ushort)i, "", true));
                    inputItems[dataType.GUID] = storedItems;

                }
            }

            //Case Parameter
            else if (obj is IGH_Param param)
            {
                param.CreateAttributes();

                if (!IsParamGeneral(param.GetType(), out Type dataType)) continue;

                if (param.Attributes.HasInputGrip)
                {
                    inputRemap[param.ComponentGuid] = dataType.GUID;

                    if (!outputItems.TryGetValue(dataType.GUID, out List<CreateObjectItem> storedItems))
                    {
                        storedItems = [];
                    }

                    bool quit = false;
                    foreach (CreateObjectItem item in storedItems)
                    {
                        if (item.ObjectGuid == obj.ComponentGuid)
                        {
                            quit = true;
                            break;
                        }
                    }
                    if (!quit)
                    {
                        storedItems.Add(new CreateObjectItem(obj.ComponentGuid, 0, "", false));
                        outputItems[dataType.GUID] = storedItems;
                    }
                }

                if (param.Attributes.HasOutputGrip)
                {
                    outputRemap[param.ComponentGuid] = dataType.GUID;

                    if (!inputItems.TryGetValue(dataType.GUID, out List<CreateObjectItem> storedItems))
                    {
                        storedItems = [];
                    }

                    bool quit = false;
                    foreach (CreateObjectItem item in storedItems)
                    {
                        if (item.ObjectGuid == obj.ComponentGuid)
                        {
                            quit = true;
                            break;
                        }
                    }
                    if (!quit)
                    {
                        storedItems.Add(new CreateObjectItem(obj.ComponentGuid, 0, "", true));
                        inputItems[dataType.GUID] = storedItems;
                    }
                }
            }

            if (obj is IDisposable dis) dis.Dispose();
        }
        //Add to InputItems.
        foreach (var pair in inputRemap)
        {
            if (inputItems.TryGetValue(pair.Value, out List<CreateObjectItem> items))
            {
                items.Sort();
                InputItems[pair.Key] = [.. items];
            }
        }
        //Add to OutputItems.
        foreach (var pair in outputRemap)
        {
            if (outputItems.TryGetValue(pair.Value, out List<CreateObjectItem> items))
            {
                items.Sort();
                OutputItems[pair.Key] = [.. items];
            }
        }

        //Add to ListItems.
        listItems.Sort();
        ListItems = [.. listItems];

        //Add to TreeItems.
        treeItems.Sort();
        TreeItems = [.. treeItems];

        Instances.DocumentEditor.SetStatusBarEvent(new GH_RuntimeMessage(
            $"Quick Connection read {createCount} obejcts successfully."
            , GH_RuntimeMessageLevel.Remark));
    }

    private static bool IsParamGeneral(Type type, out Type dataType)
    {
        dataType = null;
        if (type == null)
        {
            return false;
        }
        else if (type.IsGenericType)
        {
            if (type.GetGenericTypeDefinition() == typeof(GH_Param<>))
            {
                dataType = type.GenericTypeArguments[0];
                return true;
            }
        }
        else if (type == typeof(GH_ActiveObject))
            return false;
        return IsParamGeneral(type.BaseType, out dataType);
    }
}

internal struct CreateObjectItemsSave
{
    public SortedList<string, CreateObjectItemSave[]> InputItems { get; set; }
    public SortedList<string, CreateObjectItemSave[]> OutputItems { get; set; }
    public CreateObjectItemSave[] ListItems { get; set; }
    public CreateObjectItemSave[] TreeItems { get; set; }

    public CreateObjectItemsSave(CreateObjectItems items)
    {
        InputItems = [];
        foreach (var inputPair in items.InputItems)
        {
            CreateObjectItemSave[] inputPairSave = new CreateObjectItemSave[inputPair.Value.Length];
            for (int i = 0; i < inputPair.Value.Length; i++)
            {
                inputPairSave[i] = new CreateObjectItemSave(inputPair.Value[i]);
            }
            InputItems[inputPair.Key.ToString()] = inputPairSave;
        }

        OutputItems = [];
        foreach (var outputPair in items.OutputItems)
        {
            CreateObjectItemSave[] outputPairSave = new CreateObjectItemSave[outputPair.Value.Length];
            for (int i = 0; i < outputPair.Value.Length; i++)
            {
                outputPairSave[i] = new CreateObjectItemSave(outputPair.Value[i]);
            }
            OutputItems[outputPair.Key.ToString()] = outputPairSave;
        }

        CreateObjectItemSave[] list = new CreateObjectItemSave[items.ListItems.Length];
        for (int i = 0; i < items.ListItems.Length; i++)
        {
            list[i] = new CreateObjectItemSave(items.ListItems[i]);
        }
        ListItems = list;

        CreateObjectItemSave[] tree = new CreateObjectItemSave[items.TreeItems.Length];
        for (int i = 0; i < items.TreeItems.Length; i++)
        {
            tree[i] = new CreateObjectItemSave(items.TreeItems[i]);
        }
        TreeItems = tree;
    }
}
