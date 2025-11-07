// ===== BIẾN TOÀN CỤC =====
var selectedPdfFile = null;
var additionalFiles = [];

// ===== KHỞI TẠO KHI DOCUMENT READY =====
$(function() {
  
  // Khởi tạo Summernote
  $('#summernote').summernote({
    placeholder: 'Viết nội dung báo cáo ở đây...',
    tabsize: 2,
    height: 300,
    toolbar: [
      ['style', ['style']],
      ['font', ['bold', 'underline', 'clear']],
      ['color', ['color']],
      ['para', ['ul', 'ol', 'paragraph']],
      ['table', ['table']],
      ['insert', ['link']],
      ['view', ['fullscreen', 'codeview', 'help']]
    ]
  });
  
  // Event: Chọn file PDF
  $('#pdfFile').on('change', handlePdfFileSelect);
  
  // Event: Submit form
  $('#submitForm').on('submit', handleSubmitReport);
  
  console.log('Submit.js đã được khởi tạo');
  
});

// ===== HÀM XỬ LÝ LOGIC =====

/**
 * Xử lý chọn file PDF
 */
function handlePdfFileSelect() {
  var file = this.files[0];
  
  if (!file) {
    selectedPdfFile = null;
    $('#pdfPreview').empty();
    return;
  }
  
  // Kiểm tra file PDF
  if (file.type !== 'application/pdf') {
    showToast('Vui lòng chọn file PDF!', 'warning');
    $(this).val('');
    return;
  }
  
  // Kiểm tra kích thước (max 10MB)
  if (file.size > 10 * 1024 * 1024) {
    showToast('File PDF quá lớn! Tối đa 10MB', 'warning');
    $(this).val('');
    return;
  }
  
  selectedPdfFile = file;
  
  // Hiển thị preview
  renderPdfPreview(file);
  updateChecklist();
  showToast('Đã chọn file PDF thành công!', 'success');
}

/**
 * Hiển thị preview file PDF
 */
function renderPdfPreview(file) {
  var fileSize = (file.size / 1024).toFixed(2);
  var preview = $('<div class="file-item"></div>');
  preview.html(`
    <div class="file-info">
      <div class="file-icon" style="background: #ef4444;">
        <i class="fas fa-file-pdf"></i>
      </div>
      <div class="file-details">
        <div class="file-name">${file.name}</div>
        <div class="file-size">${fileSize} KB</div>
      </div>
    </div>
    <div class="file-actions">
      <button type="button" class="btn btn-sm btn-outline-success me-1" onclick="previewPdf()">
        <i class="fas fa-eye"></i> Xem
      </button>
      <button type="button" class="btn btn-sm btn-outline-danger" onclick="removePdfFile()">
        <i class="fas fa-times"></i> Xóa
      </button>
    </div>
  `);
  
  $('#pdfPreview').html(preview);
}

/**
 * Xóa file PDF đã chọn
 */
function removePdfFile() {
  selectedPdfFile = null;
  $('#pdfFile').val('');
  $('#pdfPreview').empty();
  updateChecklist();
  showToast('Đã xóa file PDF!', 'info');
}

/**
 * Xử lý chọn file đính kèm thêm
 */
function handleAdditionalFilesSelect() {
  var files = this.files;
  additionalFiles = Array.from(files);
  
  var $list = $('#additionalFilesList');
  $list.empty();
  
  if (files.length === 0) {
    return;
  }
  
  for (var i = 0; i < files.length; i++) {
    var file = files[i];
    var fileSize = (file.size / 1024).toFixed(2);
    var fileItem = $('<div class="file-item"></div>');
    
    fileItem.html(`
      <div class="file-info">
        <div class="file-icon">
          <i class="fas fa-file"></i>
        </div>
        <div class="file-details">
          <div class="file-name">${file.name}</div>
          <div class="file-size">${fileSize} KB</div>
        </div>
      </div>
      <button type="button" class="file-remove" data-index="${i}">
        <i class="fas fa-times"></i>
      </button>
    `);
    
    $list.append(fileItem);
  }
}


/**
 * Xử lý gửi báo cáo
 */
function handleSubmitReport(e) {
  e.preventDefault();
  
  // Validate
  var department = $('#department').val().trim();
  var week = $('#week').val().trim();
  var toEmail = $('#toEmail').val().trim();
  var content = $('#summernote').summernote('code');
  
  if (!department || department === "Chưa đăng nhập") {
    showToast('Lỗi! Không tìm thấy thông tin bộ phận. Vui lòng đăng nhập lại.', 'warning');
    $('#department').focus();
    return;
  }
  
  if (!week) {
    showToast('Lui lòng nhập tuần báo cáo!', 'warning');
    $('#week').focus();
    return;
  }
  
  if (!toEmail) {
    showToast('Vui lòng nhập email người nhận!', 'warning');
    $('#toEmail').focus();
    return;
  }
  
  if (!isValidEmail(toEmail)) {
    showToast('Email người nhận không hợp lệ!', 'warning');
    $('#toEmail').focus();
    return;
  }
  
  // Validate độ dài các trường
  if (toEmail.length > 256) {
    showToast('Email người nhận không được vượt quá 256 ký tự!', 'warning');
    $('#toEmail').focus();
    return;
  }
  
  var ccEmail = $('#ccEmail').val().trim();
  if (ccEmail.length > 1000) {
    showToast('Email CC không được vượt quá 1000 ký tự!', 'warning');
    $('#ccEmail').focus();
    return;
  }
  
  var subject = 'Báo cáo tuần - ' + department;
  if (subject.length > 255) {
    showToast('Tiêu đề email không được vượt quá 255 ký tự! Vui lòng rút ngắn tên bộ phận.', 'warning');
    $('#department').focus();
    return;
  }
  
  if (department.length > 100) {
    showToast('Tên bộ phận không được vượt quá 100 ký tự!', 'warning');
    $('#department').focus();
    return;
  }
  
  if (!content || content.trim() === '' || content === '<p><br></p>') {
    showToast('Vui lòng nhập nội dung báo cáo!', 'warning');
    return;
  }
  
  if (!selectedPdfFile) {
    showToast('Vui lòng đính kèm file PDF báo cáo!', 'warning');
    $('#pdfFile').focus();
    return;
  }
  
  // Validate độ dài tên file (đường dẫn sẽ được tạo ở server)
  if (selectedPdfFile.name.length > 400) {
    showToast('Tên file PDF quá dài! Vui lòng đổi tên file ngắn hơn.', 'warning');
    $('#pdfFile').focus();
    return;
  }
  
  // Hiển thị loading
  showLoading('Đang upload file...');

  //  Upload file theo chunks 500KB
  // SỬA: Bỏ `department` đi, vì API sẽ lấy từ Session
  uploadFileInChunks(selectedPdfFile, toEmail, ccEmail, subject, content);
}

/**
 * Upload file theo chunks 500KB
 */
async function uploadFileInChunks(file, toEmail, ccEmail, subject, content) {
  const CHUNK_SIZE = 500 * 1024; // 500KB
  const totalChunks = Math.ceil(file.size / CHUNK_SIZE);
  const uploadId = generateUploadId();
  
  try {
    // Gửi từng chunk
    for (let i = 0; i < totalChunks; i++) {
      const start = i * CHUNK_SIZE;
      const end = Math.min(start + CHUNK_SIZE, file.size);
      const chunk = file.slice(start, end);
      
      const formData = new FormData();
      formData.append('uploadId', uploadId);
      formData.append('originalFileName', file.name);
      formData.append('chunkIndex', i);
      formData.append('totalChunks', totalChunks);
      formData.append('totalSizeBytes', file.size);
      formData.append('chunk', chunk);
      
      // Update loading message
      showLoading(`Đang upload chunk ${i + 1}/${totalChunks}...`);
      
      const response = await $.ajax({
        url: '/api/email/upload-chunk',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false
      });
      
      // Nếu là chunk cuối → gửi email
      if (response.completed && response.file) {
        showLoading('Đang gửi email...');
        
        // SỬA: Gửi tất cả thông tin file (khớp với EmailCommand)
        await sendEmailWithUploadedFile(
            response.file.filePath,         // (vd: /media/file_guid.pdf)
            response.file.fileName,         // (vd: BaoCaoGoc.pdf)
            response.file.fileExtension,    // (vd: .pdf)
            response.file.fileSizeKB,       // (vd: 1024)
            toEmail, 
            ccEmail, 
            subject, 
            content
        );
        return;
      }
    }
  } catch (error) {
    hideLoading();
    var errorMessage = 'Lỗi upload file!';
    if (error.responseJSON && error.responseJSON.message) {
      errorMessage = error.responseJSON.message;
    } else if (error.responseText) {
      try {
        var errorData = JSON.parse(error.responseText);
        if (errorData.message) {
          errorMessage = errorData.message;
        }
      } catch (e) {
        // Không parse được JSON, dùng message mặc định
      }
    }
    showToast(errorMessage, 'danger');
  }
}

/**
 * Gửi email sau khi upload file xong
 * SỬA: Hàm này không cần `department` nữa (vì API lấy từ Session)
 */
async function sendEmailWithUploadedFile(publicFilePath, originalFileName, fileExtension, fileSizeKB, toEmail, ccEmail, subject, content) {
  try {
    
    var formData = new FormData();
    formData.append('ToEmail', toEmail);
    formData.append('CCEmail', ccEmail);
    formData.append('Subject', subject);
    formData.append('Content', content);
    
    // (Bỏ CreatedBy, API sẽ lấy từ Session)
    
    // Thông tin file (để lưu vào DB)
    formData.append('FilePath', publicFilePath); 
    formData.append('OriginalFileName', originalFileName);
    formData.append('FileExtension', fileExtension || '.pdf');
    formData.append('FileSizeKB', fileSizeKB || 0);

    formData.append('Status', 0); // (Trạng thái Pending ban đầu)
    
    const response = await $.ajax({
      url: '/api/email/send',
      type: 'POST',
      data: formData,
      processData: false,
      contentType: false
    });
    
    hideLoading();
    showToast(response.message || 'Gửi email thành công!', 'success');
    showSuccessModal();
  } catch (error) {
    hideLoading();
    var errorMessage = 'Lỗi gửi email!';
    if (error.responseJSON && error.responseJSON.message) {
      errorMessage = error.responseJSON.message;
    } else if (error.responseText) {
      try {
        var errorData = JSON.parse(error.responseText);
        if (errorData.message) {
          errorMessage = errorData.message;
        }
      } catch (e) {
        // Không parse được JSON, dùng message mặc định
      }
    }
    showToast(errorMessage, 'danger');
  }
}

/**
 * Tạo upload ID duy nhất
 */
function generateUploadId() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
    var r = Math.random() * 16 | 0;
    var v = c == 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}

/**
 * Hiển thị modal thành công
 */
function showSuccessModal() {
  var successHtml = `
    <div class="modal fade" id="successModal" tabindex="-1">
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
          <div class="modal-body text-center py-5">
            <div class="success-icon mb-4">
              <i class="fas fa-check-circle" style="font-size: 80px; color: #10b981;"></i>
            </div>
            <h3 class="mb-3">Gửi báo cáo thành công!</h3>
            <p class="text-muted mb-4">
              Báo cáo của bạn đã được gửi đến người quản lý.<br>
              Bạn có thể xem lại trong "Lịch sử báo cáo".
            </p>
            <div class="d-flex gap-2 justify-content-center">
              <a href="/Home/Index" class="btn btn-primary">
                <i class="fas fa-home me-1"></i>Về trang chủ
              </a>
              <a href="/Report/Submit" class="btn btn-outline-secondary">
                <i class="fas fa-plus me-1"></i>Tạo báo cáo mới
              </a>
            </div>
          </div>
        </div>
      </div>
    </div>
  `;
  
  $('body').append(successHtml);
  var modal = new bootstrap.Modal(document.getElementById('successModal'));
  modal.show();
  
  // Xóa modal sau khi đóng
  $('#successModal').on('hidden.bs.modal', function() {
    $(this).remove();
  });
}

/**
 * Xem PDF
 */
function previewPdf() {
  if (selectedPdfFile) {
    var url = URL.createObjectURL(selectedPdfFile);
    window.open(url, '_blank');
  }
}

/**
 * Update checklist
 */
function updateChecklist() {
  var $items = $('.checklist-item');
  if (!$items.length) return; // Thoát nếu không ở trang có checklist
  
  // Check PDF file
  if (selectedPdfFile) {
    $items.eq(3).removeClass('pending').find('i')
      .removeClass('fa-circle text-warning')
      .addClass('fa-check-circle text-success');
  }
  
  // Check email
  if ($('#toEmail').val().trim()) {
    $items.eq(4).removeClass('pending').find('i')
      .removeClass('fa-circle text-warning')
      .addClass('fa-check-circle text-success');
  }
}

/**
 * Validate email
 */
function isValidEmail(email) {
  var regex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return regex.test(email);
}

/**
 * Show toast (sử dụng từ main.js)
 */
function showToast(message, type) {
  if (window.AppUtils && window.AppUtils.showToast) {
    window.AppUtils.showToast(message, type);
  } else {
    alert(message);
  }
}

/**
 * Show loading (sử dụng từ main.js)
 */
function showLoading(message) {
  if (window.AppUtils && window.AppUtils.showPageLoading) {
    window.AppUtils.showPageLoading(message); // Truyền message vào
  } else {
    // Fallback loading đơn giản
    if (message) {
      if (!$('#simpleLoading').length) {
        var loadingHtml = `
          <div id="simpleLoading" style="position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.7); z-index: 9999; display: flex; align-items: center; justify-content: center;">
            <div style="background: white; padding: 30px; border-radius: 10px; text-align: center;">
              <div class="spinner-border text-primary mb-3" role="status">
                <span class="visually-hidden">Loading...</span>
              </div>
              <div id="simpleLoadingText" style="font-size: 16px; color: #333;">${message}</div>
            </div>
          </div>
        `;
        $('body').append(loadingHtml);
      } else {
        $('#simpleLoadingText').text(message);
      }
    }
  }
}

/**
 * Hide loading (sử dụng từ main.js)
 */
function hideLoading() {
  if (window.AppUtils && window.AppUtils.hidePageLoading) {
    window.AppUtils.hidePageLoading();
  }
  
  // Xóa simple loading nếu có
  $('#simpleLoading').remove();
}

// Event: Update checklist khi nhập email
$(document).on('input', '#toEmail', updateChecklist);