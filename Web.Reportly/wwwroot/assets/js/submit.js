// ===== submit.js (Final Version) =====
var selectedUploadId = null; // ID file từ server (sau khi CreateReportDraft)
var selectedLocalFile = null;   // File PDF chọn từ máy

$(function() {
    $('#summernote').summernote({
        placeholder: 'Viết nội dung báo cáo ở đây...',
        tabsize: 2,
        height: 300,
    });
 

    // Kiểm tra và load draft nếu có tham số
    if (window.preloadDraft) {
        window.updatePdfPreviewFromId(window.preloadDraft.id, window.preloadDraft.fileName, window.preloadDraft.filePath);
        if(window.AppUtils && window.AppUtils.showToast) {
             window.AppUtils.showToast('Đã tải bản nháp. Hãy kiểm tra và gửi đi.', 'info');
        }
    }

    $('#pdfFile').on('change', handleLocalFileSelect);
    $('#submitForm').on('submit', handleSubmit);
});

// Hàm được gọi từ report.js sau khi tạo báo cáo nháp thành công
window.updatePdfPreviewFromId = function(uploadId, fileName, filePath) {
    selectedUploadId = uploadId;
    selectedLocalFile = null; 
    $('#pdfFile').val(''); 

    // Render preview UI
    $('#pdfPreview').html(`
        <div class="alert alert-success d-flex align-items-center">
            <i class="fas fa-check-circle me-2 fa-2x"></i>
            <div>
                <strong>Đã sẵn sàng gửi!</strong><br>
                File: <a href="${filePath}" target="_blank">${fileName}</a> (Đã lưu trên server)
            </div>
        </div>
    `);
    
    if (typeof updateChecklist === 'function') updateChecklist();
};

function handleLocalFileSelect(e) {
    const file = e.target.files[0];
    if (!file) return;
    
    //  Gọi qua AppUtils
    if (file.type !== 'application/pdf') {
        window.AppUtils.showToast('Chỉ chấp nhận file PDF', 'warning');
        $(this).val('');
        return;
    }

    selectedLocalFile = file;
    selectedUploadId = null; 
    $('#pdfPreview').html(`
        <div class="alert alert-info">
            <i class="fas fa-file-pdf me-2"></i> Đã chọn: <strong>${file.name}</strong>
            <br><small>(Sẽ được upload khi nhấn Gửi)</small>
        </div>
    `);
    
    if (typeof updateChecklist === 'function') updateChecklist();
}

async function handleSubmit(e) {
    e.preventDefault();

    if (!selectedUploadId && !selectedLocalFile) {
        window.AppUtils.showToast('Vui lòng "Tạo báo cáo" hoặc chọn file PDF!', 'warning');
        return;
    }

    const toEmail = $('#toEmail').val().trim();
    if (!toEmail) { window.AppUtils.showToast('Thiếu email người nhận', 'warning'); return; }

    window.AppUtils.showPageLoading('Đang xử lý...');

    try {
        if (selectedUploadId) {
            //  Gửi ngay với UploadId đã có
            await sendEmail({ UploadId: selectedUploadId, ToEmail: toEmail });
        } else if (selectedLocalFile) {
             // Upload chunk rồi mới gửi ==============
             const uploadResult = await uploadFile(selectedLocalFile); 
             if (uploadResult && uploadResult.uploadId) {
                 await sendEmail({ UploadId: uploadResult.uploadId, ToEmail: toEmail });
             } else {
                 throw new Error("Lỗi upload file không xác định.");
             }
        }
    } catch (error) {
        let errorMessage = error.message || 'Lỗi không xác định.';
        if (error.responseJSON && error.responseJSON.message) {
            errorMessage = error.responseJSON.message;
        } else if (error.responseText) {
             errorMessage = error.responseText;
        }
        window.AppUtils.showToast('Lỗi: ' + errorMessage, 'error');
    } finally {
        window.AppUtils.hidePageLoading();
    }
}

// CHUNKING DÙNG AJAX ---
async function uploadFile(file) {
    const CHUNK_SIZE = 500 * 1024; // 500KB
    const totalChunks = Math.ceil(file.size / CHUNK_SIZE);
    const uploadId = generateUploadId(); 
    
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
        
        window.AppUtils.showPageLoading(`Đang upload file đính kèm: ${i + 1}/${totalChunks}...`);
        
        try {
            const response = await $.ajax({
                url: '/api/email/upload-chunk',
                type: 'POST',
                data: formData,
                processData: false,
                contentType: false
            });

            if (response.completed && response.file) {
                return { uploadId: response.file.id, file: response.file }; 
            }
        } catch (error) {
            throw new Error(error.responseJSON?.message || error.statusText || 'Lỗi upload chunk.');
        }
    }
    return null;
}

// TẠO UUID CHO CHUNKING ---
function generateUploadId() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
    var r = Math.random() * 16 | 0;
    var v = c == 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}

// Hàm gửi email 
async function sendEmail(data) {
    const formData = new FormData();
    for (const key in data) {
        formData.append(key, data[key]);
    }
    
    // Append các trường còn thiếu từ form
    formData.append('CCEmail', $('#ccEmail').val() || '');
    formData.append('Subject', $('#department').val() + ' - Báo cáo tuần'); 
    formData.append('Content', $('#summernote').summernote('code'));

    return new Promise((resolve, reject) => {
        $.ajax({
            url: '/api/email/send',
            type: 'POST',
            data: formData,
            processData: false, 
            contentType: false, 
            success: function(result) {
                showSuccessModal(); 
                resolve(result);
            },
            error: function(jqXHR, textStatus, errorThrown) {
                let errorMessage = 'Lỗi server.';
                if (jqXHR.responseJSON && jqXHR.responseJSON.message) {
                    errorMessage = jqXHR.responseJSON.message;
                } else if (jqXHR.responseText) {
                    errorMessage = jqXHR.responseText;
                }
                reject(new Error(errorMessage));
            }
        });
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


$(document).on('input', '#toEmail', updateChecklist);