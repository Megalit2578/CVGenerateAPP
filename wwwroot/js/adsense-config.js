/**
 * ════════════════════════════════════════════════════════════════
 *  GOOGLE ADSENSE CONFIGURATION
 *  Publisher ID: ca-pub-4398389720271327  ✅ ACTIVE
 *
 *  Auto Ads: Bật tự động qua script trong <head> — Google tự đặt ads.
 *
 *  Manual Slots: Điền slot IDs bên dưới sau khi tạo ad units tại:
 *  AdSense → Quảng cáo → Theo đơn vị quảng cáo → Tạo đơn vị mới
 * ════════════════════════════════════════════════════════════════
 */

window.ADSENSE_CONFIG = {
  publisherId: 'ca-pub-4398389720271327',  // ✅ Real publisher ID

  slots: {
    // Tạo ad unit "Leaderboard 728×90"  → dán Slot ID vào đây
    topBanner:    '',

    // Tạo ad unit "Medium Rectangle 300×250" → dán Slot ID vào đây
    sidebarBox:   '',

    // Tạo ad unit "Leaderboard 728×90"  → dán Slot ID vào đây
    bottomBanner: '',
  },

  responsive: true,
};

/*
 *  VÍ DỤ KHI CÓ SLOT IDs:
 *
 *  slots: {
 *    topBanner:    '1234567890',
 *    sidebarBox:   '0987654321',
 *    bottomBanner: '1122334455',
 *  }
 *
 *  → git add . && git commit -m "Add slot IDs" && git push
 *  → Railway tự deploy lại trong ~1 phút
 */
