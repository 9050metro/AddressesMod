﻿using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.UI;
using ICities;
using Klyte.Extensions;
using Klyte.Harmony;
using Klyte.Commons.Extensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Klyte.Addresses.Utils;
using System.IO;
using Klyte.Commons.Overrides;

namespace Klyte.Addresses.UI
{

    internal class AdrConfigPanel : UICustomControl
    {
        private const int NUM_SERVICES = 0;
        private static AdrConfigPanel instance;

        public UIPanel controlContainer { get; private set; }
        private UIPanel mainPanel;

        private UITabstrip m_StripMain;

        private UIDropDown m_selectDistrict;
        private Dictionary<string, int> m_cachedDistricts;
        private string m_lastSelectedItem;
        private UIDropDown m_districtNameFile;
        private UIDropDown m_prefixesFile;

        private UIHelperExtension m_uiHelperDistrict;
        private UIHelperExtension m_uiHelperHighway;
        private UIHelperExtension m_uiHelperGlobal;

        public static AdrConfigPanel Get()
        {
            if (instance)
            {
                return instance;
            }
            UIView view = FindObjectOfType<UIView>();
            AdrUtils.createUIElement(out UIPanel panelObj, view.transform);

            return instance = panelObj.gameObject.AddComponent<AdrConfigPanel>();
        }

        #region Awake
        private void Awake()
        {
            controlContainer = GetComponent<UIPanel>();
            controlContainer.area = new Vector4(0, 0, 0, 0);
            controlContainer.isVisible = false;
            controlContainer.name = "AdrPanel";

            AdrUtils.createUIElement(out mainPanel, controlContainer.transform, "AdrListPanel", new Vector4(395, 58, 400, 600));
            mainPanel.backgroundSprite = "MenuPanel2";
            CreateTitleBar();

            AdrUtils.createUIElement(out m_StripMain, mainPanel.transform, "AdrTabstrip", new Vector4(5, 45, mainPanel.width - 10, 40));

            AdrUtils.createUIElement(out UITabContainer tabContainer, mainPanel.transform, "AdrTabContainer", new Vector4(0, 80, mainPanel.width, mainPanel.height - 80));
            m_StripMain.tabPages = tabContainer;

            m_uiHelperDistrict = CreateTab("ToolbarIconDistrict", "ADR_CONFIG_PER_DISTRICT_TAB", "AdrPerDistrict");
            //m_uiHelperHighway = CreateTab("SubBarRoadsHighway", "ADR_CONFIG_HIGHWAY_TAB", "AdrHighway");
            m_uiHelperGlobal = CreateTab("ToolbarIconZoomOutGlobe", "ADR_CONFIG_GLOBAL_TAB", "AdrGlobal");
            PopulateTab1();
            PopulateTab3();

            DistrictManagerOverrides.eventOnDistrictRenamed += reloadDistricts;
            reloadDistricts();
        }

        private void PopulateTab1()
        {
            ((UIScrollablePanel)m_uiHelperDistrict.self).autoLayoutDirection = LayoutDirection.Horizontal;
            ((UIScrollablePanel)m_uiHelperDistrict.self).wrapLayout = true;

            m_cachedDistricts = AdrUtils.getValidDistricts();
            m_selectDistrict = m_uiHelperDistrict.AddDropdownLocalized("ADR_DISTRICT_TITLE", m_cachedDistricts.Keys.OrderBy(x => x).ToArray(), 0, OnDistrictSelect);
            m_selectDistrict.width = 370;
            m_uiHelperDistrict.AddSpace(30);

            m_districtNameFile = m_uiHelperDistrict.AddDropdownLocalized("ADR_DISTRICT_NAME_FILE", new String[0], -1, onChangeSelectedRoadName);
            m_districtNameFile.width = 370;
            AdrUtils.LimitWidth((UIButton)m_uiHelperDistrict.AddButton(Locale.Get("ADR_ROAD_NAME_FILES_RELOAD"), reloadOptionsRoad), 380);

            m_prefixesFile = m_uiHelperDistrict.AddDropdownLocalized("ADR_STREETS_PREFIXES_NAME_FILE", new String[0], -1, onChangeSelectedRoadPrefix);
            m_prefixesFile.width = 370;
            AdrUtils.LimitWidth((UIButton)m_uiHelperDistrict.AddButton(Locale.Get("ADR_STREETS_PREFIXES_FILES_RELOAD"), reloadOptionsRoadPrefix), 380);
        }

        private void PopulateTab3()
        {
            ((UIScrollablePanel)m_uiHelperGlobal.self).autoLayoutDirection = LayoutDirection.Horizontal;
            ((UIScrollablePanel)m_uiHelperGlobal.self).wrapLayout = true;
        }

        private void reloadOptionsRoad()
        {
            if (getSelectedConfigIndex() < 0) return;
            AdrController.reloadLocalesRoad();
            AdrConfigWarehouse.ConfigIndex currentSelectedDistrict = (AdrConfigWarehouse.ConfigIndex)(getSelectedConfigIndex());
            List<string> items = AdrController.loadedLocalesRoadName.Keys.ToList();
            items.Insert(0, Locale.Get(m_selectDistrict.selectedIndex == 0 ? "ADR_DEFAULT_FILE_NAME" : "ADR_DEFAULT_CITY_FILE_NAME"));
            m_districtNameFile.items = items.ToArray();
            string filename = AdrConfigWarehouse.getCurrentConfigString(AdrConfigWarehouse.ConfigIndex.ROAD_NAME_FILENAME | currentSelectedDistrict);
            if (items.Contains(filename))
            {
                m_districtNameFile.selectedValue = filename;
            }
            else
            {
                m_districtNameFile.selectedIndex = 0;
            }
        }

        private void reloadOptionsRoadPrefix()
        {
            if (getSelectedConfigIndex() < 0) return;
            AdrController.reloadLocalesRoadPrefix();
            List<string> items = AdrController.loadedLocalesRoadPrefix.Keys.ToList();
            AdrConfigWarehouse.ConfigIndex currentSelectedDistrict = (AdrConfigWarehouse.ConfigIndex)(getSelectedConfigIndex());
            items.Insert(0, Locale.Get(m_selectDistrict.selectedIndex == 0 ? "ADR_DEFAULT_FILE_NAME_PREFIX" : "ADR_DEFAULT_CITY_FILE_NAME_PREFIX"));
            m_prefixesFile.items = items.ToArray();
            string filename = AdrConfigWarehouse.getCurrentConfigString(AdrConfigWarehouse.ConfigIndex.PREFIX_FILENAME | currentSelectedDistrict);
            if (items.Contains(filename))
            {
                m_prefixesFile.selectedValue = filename;
            }
            else
            {
                m_prefixesFile.selectedIndex = 0;
            }
        }


        private void onChangeSelectedRoadPrefix(int idx)
        {
            setDistrictProperty(AdrConfigWarehouse.ConfigIndex.PREFIX_FILENAME, idx > 0 ? m_prefixesFile.selectedValue : null);
        }

        private void onChangeSelectedRoadName(int idx)
        {
            setDistrictProperty(AdrConfigWarehouse.ConfigIndex.ROAD_NAME_FILENAME, idx > 0 ? m_districtNameFile.selectedValue : null);
        }

        private void setDistrictProperty(AdrConfigWarehouse.ConfigIndex configIndex, string value)
        {
            AdrConfigWarehouse.ConfigIndex selectedDistrict;
            if (m_cachedDistricts.ContainsKey(m_selectDistrict.selectedValue))
            {
                selectedDistrict = (AdrConfigWarehouse.ConfigIndex)(m_cachedDistricts[m_selectDistrict.selectedValue] & 0xFF);
            }
            else if (m_selectDistrict.selectedIndex == 0)
            {
                selectedDistrict = 0;
            }
            else
            {
                return;
            }
            AdrConfigWarehouse.setCurrentConfigString(configIndex | selectedDistrict, value);
            AdrUtils.UpdateSegmentNamesView();
        }

        private int getSelectedConfigIndex()
        {
            if (m_cachedDistricts.ContainsKey(m_selectDistrict.selectedValue))
            {
                return (m_cachedDistricts[m_selectDistrict.selectedValue] & 0xFF);
            }
            else if (m_selectDistrict.selectedIndex == 0)
            {
                return 0;
            }
            else
            {
                return -1;
            }
        }

        private UIHelperExtension CreateTab(string sprite, string localeKey, string objectName)
        {
            UIButton tab = CreateTabTemplate();
            tab.normalFgSprite = sprite;
            tab.tooltip = Locale.Get(localeKey);

            AdrUtils.createUIElement(out UIPanel contentContainer, null);
            contentContainer.name = "Container";
            contentContainer.area = new Vector4(5, 0, mainPanel.width - 10, mainPanel.height - 70);
            m_StripMain.AddTab(objectName, tab.gameObject, contentContainer.gameObject);

            return AdrUtils.CreateScrollPanel(contentContainer, out UIScrollablePanel scrollablePanel, out UIScrollbar scrollbar, contentContainer.width - 20, contentContainer.height - 5, new Vector3());
        }

        private void OnDistrictSelect(int x)
        {
            String oldSel = m_lastSelectedItem;
            try
            {
                m_lastSelectedItem = m_selectDistrict.items[x];
            }
            catch
            {
                if (m_selectDistrict.items.Length > 0)
                {
                    m_lastSelectedItem = m_selectDistrict.items[0];
                }
                else
                {
                    m_lastSelectedItem = null;
                    m_selectDistrict.selectedIndex = -1;
                }
            }
            //load district info
            reloadOptionsRoadPrefix();
            reloadOptionsRoad();
        }


        private void reloadDistricts()
        {
            m_cachedDistricts = AdrUtils.getValidDistricts();
            m_selectDistrict.items = m_cachedDistricts.Keys.OrderBy(x => x).ToArray();
            m_selectDistrict.selectedValue = m_lastSelectedItem;
            AdrUtils.UpdateSegmentNamesView();
        }

        public int getCurrentSelectedDistrictId()
        {
            if (m_lastSelectedItem == null || !m_cachedDistricts.ContainsKey(m_lastSelectedItem))
            {
                return -1;
            }
            return m_cachedDistricts[m_lastSelectedItem];
        }

        private static UIButton CreateTabTemplate()
        {
            AdrUtils.createUIElement(out UIButton tabTemplate, null, "AdrTabTemplate");
            AdrUtils.initButton(tabTemplate, false, "GenericTab");
            tabTemplate.autoSize = false;
            tabTemplate.height = 30;
            tabTemplate.foregroundSpriteMode = UIForegroundSpriteMode.Scale;
            return tabTemplate;
        }

        private void CreateTitleBar()
        {
            AdrUtils.createUIElement(out UILabel titlebar, mainPanel.transform, "AdrListPanel", new Vector4(75, 10, mainPanel.width - 150, 20));
            titlebar.autoSize = false;
            titlebar.text = "Addresses v" + AddressesMod.version;
            titlebar.textAlignment = UIHorizontalAlignment.Center;
            AdrUtils.createDragHandle(titlebar, mainPanel);

            AdrUtils.createUIElement(out UIButton closeButton, mainPanel.transform, "CloseButton", new Vector4(mainPanel.width - 37, 5, 32, 32));
            AdrUtils.initButton(closeButton, false, "buttonclose", true);
            closeButton.hoveredBgSprite = "buttonclosehover";
            closeButton.eventClick += (x, y) =>
            {
                AdrController.instance.CloseAdrPanel();
            };

            AdrUtils.createUIElement(out UISprite logo, mainPanel.transform, "AddressesIcon", new Vector4(22, 5f, 32, 32));
            logo.atlas = AdrController.taAdr;
            logo.spriteName = "AddressesIcon";
            AdrUtils.createDragHandle(logo, mainPanel);
        }

        private static UIComponent CreateContentTemplate(float width, float height)
        {
            AdrUtils.createUIElement(out UIPanel contentContainer, null);
            contentContainer.name = "Container";
            contentContainer.area = new Vector4(0, 0, width, height);
            contentContainer.width = contentContainer.width - 20f;
            contentContainer.height = contentContainer.height;
            contentContainer.autoLayoutDirection = LayoutDirection.Vertical;
            contentContainer.autoLayoutStart = LayoutStart.TopLeft;
            contentContainer.autoLayoutPadding = new RectOffset(0, 0, 0, 0);
            contentContainer.autoLayout = true;
            contentContainer.clipChildren = true;
            contentContainer.relativePosition = new Vector3(5, 0);
            return contentContainer;
        }
        #endregion


        public void SetActiveTab(int idx)
        {
            m_StripMain.selectedIndex = idx;
        }

        private void Start()
        {
            SetActiveTab(-1);
            SetActiveTab(0);
        }
    }


}
