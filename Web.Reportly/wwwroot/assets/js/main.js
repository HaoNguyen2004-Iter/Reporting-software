
$(function() {
  
  // Khôi phục trạng thái sidebar
  restoreSidebarState();
  
  // Xử lý active menu
  handleActiveMenu();
  
  // Khởi tạo tooltips
  initTooltips();
  
  // Khởi tạo popovers
  initPopovers();
  
  // Xử lý back to top
  handleBackToTop();
  
  // Event: Toggle sidebar
  $('#toggleSidebar, .btn-toggle-sidebar').on('click', function(e) {
    e.preventDefault();
    
    // Check if mobile
    if ($(window).width() < 768) {
      toggleMobileSidebar();
    } else {
      toggleSidebar();
    }
  });
  
  // Event: Click outside sidebar on mobile
  $(document).on('click', function(e) {
    if ($(window).width() < 768) {
      var $sidebar = $('#sidebar');
      if ($sidebar.hasClass('show') && !$(e.target).closest('#sidebar, .btn-toggle-sidebar').length) {
        $sidebar.removeClass('show');
      }
    }
  });
  
  // Event: Resize window
  $(window).on('resize', debounce(function() {
    if ($(window).width() >= 768) {
      $('#sidebar').removeClass('show');
    }
  }, 250));
  
  // Event: Smooth scroll for anchor links
  $('a[href^="#"]').on('click', function(e) {
    var target = $(this).attr('href');
    if (target !== '#' && $(target).length) {
      e.preventDefault();
      handleSmoothScroll(target);
    }
  });
  
  // Event: Copy buttons
  $('.btn-copy').on('click', function() {
    var text = $(this).data('copy-text');
    if (text) {
      copyToClipboard(text);
    }
  });
  
  // Prevent dropdown from closing when clicking inside
  $('.dropdown-menu').on('click', function(e) {
    e.stopPropagation();
  });
  
  // Auto-hide alerts
  $('.alert[data-auto-hide]').each(function() {
    var $alert = $(this);
    var delay = $alert.data('auto-hide') || 5000;
    setTimeout(function() {
      $alert.fadeOut(300, function() {
        $(this).remove();
      });
    }, delay);
  });
  
  // Form validation helper
  $('form[data-validate]').on('submit', function(e) {
    var isValid = true;
    
    $(this).find('[required]').each(function() {
      var $field = $(this);
      var value = $field.val().trim();
      
      if (!value) {
        isValid = false;
        $field.addClass('is-invalid');
        
        if (!$field.next('.invalid-feedback').length) {
          $field.after('<div class="invalid-feedback">Trường này là bắt buộc</div>');
        }
      } else {
        $field.removeClass('is-invalid');
      }
      
      // Email validation
      if ($field.attr('type') === 'email' && value) {
        if (!isValidEmail(value)) {
          isValid = false;
          $field.addClass('is-invalid');
          
          if (!$field.next('.invalid-feedback').length) {
            $field.after('<div class="invalid-feedback">Email không hợp lệ</div>');
          }
        }
      }
    });
    
    if (!isValid) {
      e.preventDefault();
      showToast('Vui lòng điền đầy đủ thông tin!', 'warning');
    }
  });
  
  // Clear validation on input
  $('form').on('input', '[required]', function() {
    $(this).removeClass('is-invalid');
    $(this).next('.invalid-feedback').remove();
  });
  
  // Add animation to cards on scroll
  if (typeof IntersectionObserver !== 'undefined') {
    var observer = new IntersectionObserver(function(entries) {
      entries.forEach(function(entry) {
        if (entry.isIntersecting) {
          entry.target.classList.add('animated');
        }
      });
    }, {
      threshold: 0.1
    });
    
    document.querySelectorAll('.card, .stat-card').forEach(function(card) {
      observer.observe(card);
    });
  }
  
  console.log('Main.js đã được khởi tạo');
  
});

// ===== HÀM XỬ LÝ LOGIC =====

/**
 * Toggle sidebar
 */
function toggleSidebar() {
  var $sidebar = $('#sidebar');
  $sidebar.toggleClass('collapsed');
  
  // Lưu trạng thái vào localStorage
  var isCollapsed = $sidebar.hasClass('collapsed');
  localStorage.setItem('sidebarCollapsed', isCollapsed);
}

/**
 * Khôi phục trạng thái sidebar
 */
function restoreSidebarState() {
  var isCollapsed = localStorage.getItem('sidebarCollapsed');
  if (isCollapsed === 'true') {
    $('#sidebar').addClass('collapsed');
  }
}

/**
 * Toggle sidebar trên mobile
 */
function toggleMobileSidebar() {
  var $sidebar = $('#sidebar');
  $sidebar.toggleClass('show');
}

/**
 * Xử lý smooth scroll
 */
function handleSmoothScroll(target) {
  $('html, body').animate({
    scrollTop: $(target).offset().top - 100
  }, 500);
}

/**
 * Format số với dấu phân cách
 */
function formatNumber(num) {
  return num.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",");
}

/**
 * Format ngày tháng
 */
function formatDate(date) {
  var d = new Date(date);
  var day = ('0' + d.getDate()).slice(-2);
  var month = ('0' + (d.getMonth() + 1)).slice(-2);
  var year = d.getFullYear();
  return day + '/' + month + '/' + year;
}

/**
 * Format thời gian
 */
function formatDateTime(date) {
  var d = new Date(date);
  var day = ('0' + d.getDate()).slice(-2);
  var month = ('0' + (d.getMonth() + 1)).slice(-2);
  var year = d.getFullYear();
  var hours = ('0' + d.getHours()).slice(-2);
  var minutes = ('0' + d.getMinutes()).slice(-2);
  return day + '/' + month + '/' + year + ' ' + hours + ':' + minutes;
}

/**
 * Hiển thị tooltip
 */
function initTooltips() {
  if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
      return new bootstrap.Tooltip(tooltipTriggerEl);
    });
  }
}

/**
 * Hiển thị popover
 */
function initPopovers() {
  if (typeof bootstrap !== 'undefined' && bootstrap.Popover) {
    var popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'));
    popoverTriggerList.map(function (popoverTriggerEl) {
      return new bootstrap.Popover(popoverTriggerEl);
    });
  }
}

/**
 * Xử lý active menu
 */
function handleActiveMenu() {
  var currentPath = window.location.pathname.toLowerCase();
  
  // Xóa tất cả active
  $('.nav-item').removeClass('active');
  
  // Kiểm tra từng menu item
  $('.nav-item').each(function() {
    var $item = $(this);
    var href = $item.attr('href');
    
    if (!href || href === '#') {
      return; // Skip menu items không có link
    }
    
    // Lấy href và so sánh với current path
    var linkPath = href.toLowerCase();
    
    // Kiểm tra match
    if (linkPath.indexOf(currentPath) !== -1 && currentPath !== '/') {
      $item.addClass('active');
      return false; // Break loop
    }
  });
  
  // Nếu không có item nào active và đang ở trang chủ, active Dashboard
  if ($('.nav-item.active').length === 0) {
    if (currentPath === '/' || currentPath === '/home' || currentPath === '/home/index' || currentPath === '') {
      $('.nav-item').first().addClass('active'); // Dashboard là item đầu tiên
    }
  }
}

/**
 * Xử lý loading page
 */
function showPageLoading() {
  var loading = $('<div class="page-loading"></div>');
  loading.html(`
    <div class="spinner-border text-primary" role="status">
      <span class="visually-hidden">Loading...</span>
    </div>
  `);
  loading.css({
    'position': 'fixed',
    'top': '0',
    'left': '0',
    'width': '100%',
    'height': '100%',
    'background': 'rgba(255,255,255,0.9)',
    'display': 'flex',
    'align-items': 'center',
    'justify-content': 'center',
    'z-index': '999999'
  });
  
  $('body').append(loading);
}

function hidePageLoading() {
  $('.page-loading').fadeOut(300, function() {
    $(this).remove();
  });
}

/**
 * Xử lý copy to clipboard
 */
function copyToClipboard(text) {
  if (navigator.clipboard && navigator.clipboard.writeText) {
    navigator.clipboard.writeText(text).then(function() {
      showToast('Đã copy vào clipboard!', 'success');
    }).catch(function(err) {
      console.error('Lỗi copy:', err);
      showToast('Không thể copy!', 'error');
    });
  } else {
    // Fallback cho trình duyệt cũ
    var textArea = document.createElement("textarea");
    textArea.value = text;
    textArea.style.position = "fixed";
    textArea.style.left = "-999999px";
    document.body.appendChild(textArea);
    textArea.select();
    try {
      document.execCommand('copy');
      showToast('Đã copy vào clipboard!', 'success');
    } catch (err) {
      console.error('Lỗi copy:', err);
      showToast('Không thể copy!', 'error');
    }
    document.body.removeChild(textArea);
  }
}

/**
 * Hiển thị toast notification
 */
function showToast(message, type) {
  var bgColor = '#4f46e5';
  var icon = 'fa-info-circle';
  
  switch(type) {
    case 'success':
      bgColor = '#10b981';
      icon = 'fa-check-circle';
      break;
    case 'warning':
      bgColor = '#f59e0b';
      icon = 'fa-exclamation-triangle';
      break;
    case 'error':
      bgColor = '#ef4444';
      icon = 'fa-times-circle';
      break;
    case 'info':
      bgColor = '#3b82f6';
      icon = 'fa-info-circle';
      break;
  }
  
  var toast = $('<div class="custom-toast"></div>');
  toast.html(`
    <i class="fas ${icon}"></i>
    <span>${message}</span>
  `);
  toast.css({
    'position': 'fixed',
    'top': '20px',
    'right': '20px',
    'background': bgColor,
    'color': '#fff',
    'padding': '15px 25px',
    'border-radius': '8px',
    'box-shadow': '0 4px 12px rgba(0,0,0,0.15)',
    'z-index': '99999',
    'display': 'flex',
    'align-items': 'center',
    'gap': '10px',
    'font-weight': '600',
    'animation': 'slideInRight 0.3s ease',
    'min-width': '250px'
  });
  
  $('body').append(toast);
  
  setTimeout(function() {
    toast.fadeOut(300, function() {
      $(this).remove();
    });
  }, 3000);
}

/**
 * Confirm dialog
 */
function confirmDialog(message, callback) {
  if (confirm(message)) {
    if (typeof callback === 'function') {
      callback();
    }
  }
}

/**
 * Xử lý back to top
 */
function handleBackToTop() {
  var $backToTop = $('#backToTop');
  
  if (!$backToTop.length) {
    $backToTop = $('<button id="backToTop" class="btn-back-to-top"><i class="fas fa-arrow-up"></i></button>');
    $backToTop.css({
      'position': 'fixed',
      'bottom': '30px',
      'right': '30px',
      'width': '50px',
      'height': '50px',
      'border-radius': '50%',
      'background': 'linear-gradient(135deg, #4f46e5, #6366f1)',
      'color': '#fff',
      'border': 'none',
      'cursor': 'pointer',
      'display': 'none',
      'align-items': 'center',
      'justify-content': 'center',
      'box-shadow': '0 4px 12px rgba(79, 70, 229, 0.3)',
      'z-index': '9999',
      'transition': 'all 0.3s ease'
    });
    
    $backToTop.on('click', function() {
      $('html, body').animate({ scrollTop: 0 }, 500);
    });
    
    $('body').append($backToTop);
  }
  
  $(window).on('scroll', function() {
    if ($(this).scrollTop() > 300) {
      $backToTop.fadeIn().css('display', 'flex');
    } else {
      $backToTop.fadeOut();
    }
  });
}

/**
 * Xử lý search
 */
function handleSearch(query) {
  console.log('Tìm kiếm:', query);
  // Implement search logic here
}

/**
 * Validate email
 */
function isValidEmail(email) {
  var regex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return regex.test(email);
}

/**
 * Validate phone
 */
function isValidPhone(phone) {
  var regex = /^[0-9]{10,11}$/;
  return regex.test(phone);
}

/**
 * Debounce function 
 */
function debounce(func, wait) {
  var timeout;
  return function executedFunction() {
    var context = this;
    var args = arguments;
    var later = function() {
      timeout = null;
      func.apply(context, args);
    };
    clearTimeout(timeout);
    timeout = setTimeout(later, wait);
  };
}


// ===== EXPORT FUNCTIONS=====
window.AppUtils = {
  toggleSidebar: toggleSidebar,
  showToast: showToast,
  showPageLoading: showPageLoading,
  hidePageLoading: hidePageLoading,
  copyToClipboard: copyToClipboard,
  confirmDialog: confirmDialog,
  formatNumber: formatNumber,
  formatDate: formatDate,
  formatDateTime: formatDateTime,
  isValidEmail: isValidEmail,
  isValidPhone: isValidPhone,
  debounce: debounce,

};
