/**
 * ════════════════════════════════════════════════════════════════
 *  GOOGLE ADSENSE CONFIGURATION
 *  Chỉnh sửa file này để bật quảng cáo thật.
 *
 *  HƯỚNG DẪN:
 *  1. Đăng ký tài khoản AdSense tại: https://adsense.google.com
 *  2. Thêm site của bạn và chờ Google duyệt (thường 1–14 ngày)
 *  3. Vào AdSense → Quảng cáo → Theo đơn vị quảng cáo → Tạo đơn vị mới
 *  4. Tạo 3 đơn vị (Top Banner, Sidebar Box, Bottom Banner)
 *  5. Điền các ID vào bên dưới và deploy lại
 * ════════════════════════════════════════════════════════════════
 */

window.ADSENSE_CONFIG = {

  // Publisher ID — lấy từ: AdSense → Tài khoản → Thông tin tài khoản
  // Ví dụ: 'ca-pub-1234567890123456'
  publisherId: '',

  // Slot IDs — mỗi đơn vị quảng cáo có 1 slot ID riêng
  slots: {
    topBanner:    '',   // Leaderboard 728×90  (đặt ở đầu trang)
    sidebarBox:   '',   // Medium Rectangle 300×250  (sidebar phải)
    bottomBanner: '',   // Leaderboard 728×90  (cuối trang)
  },

  // true = quảng cáo responsive tự điều chỉnh kích thước theo màn hình
  responsive: true,
};

/*
 *  VÍ DỤ KHI ĐÃ CÓ IDs (xoá dấu // để kích hoạt):
 *
 *  window.ADSENSE_CONFIG = {
 *    publisherId: 'ca-pub-1234567890123456',
 *    slots: {
 *      topBanner:    '9876543210',
 *      sidebarBox:   '1234509876',
 *      bottomBanner: '5678901234',
 *    },
 *    responsive: true,
 *  };
 *
 *  SAU ĐÓ chạy: dotnet publish → upload lên hosting → XONG!
 */
