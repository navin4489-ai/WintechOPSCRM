import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { UserDto, RoleDto, OutletDto, CreateUserRequest, UpdateUserRequest, ApiResponse } from '../../models';

interface StaffForm {
  id?: number;
  username: string;
  email: string;
  password: string;
  roleId?: number;
  outletId?: number;
  isActive: boolean;
}

@Component({
  selector: 'app-staff',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './staff.component.html',
  styleUrls: ['./staff.component.css']
})
export class StaffComponent implements OnInit {
  users: UserDto[] = [];
  roles: RoleDto[] = [];
  outlets: OutletDto[] = [];

  editing = false;
  saving = false;
  error = '';
  form: StaffForm = this.blank();

  constructor(private api: ApiService, public auth:AuthService) {}

  ngOnInit() {
    this.load();
  }

  load() {
    this.api.getRoles().subscribe((r: ApiResponse<RoleDto[]>) => { if (r.success) {const u=this.auth.getCurrentUser();this.roles=(r.data||[]).filter(role=>u?.roleName==='Owner'||(role.name!=='Owner'&&role.permissions.every(p=>u?.permissions.includes(p))));} });
    this.api.getOutlets().subscribe((r: ApiResponse<OutletDto[]>) => { if (r.success) this.outlets = r.data || []; });
    this.api.getUsers().subscribe((r: ApiResponse<UserDto[]>) => { if (r.success) this.users = r.data || []; });
  }

  blank(): StaffForm {
    return { username: '', email: '', password: '', roleId: undefined, outletId: undefined, isActive: true };
  }

  newStaff() {
    this.form = this.blank();
    this.form.outletId=this.auth.getCurrentUser()?.outletId;
    this.error = '';
    this.editing = true;
  }

  edit(u: UserDto) {
    this.form = {
      id: u.id,
      username: u.username,
      email: u.email,
      password: '',
      roleId: this.roles.find(r => r.name === u.roleName)?.id,
      outletId: u.outletId,
      isActive: u.isActive
    };
    this.error = '';
    this.editing = true;
  }

  cancel() {
    this.editing = false;
    this.error = '';
  }

  save() {
    if (!this.form.username || !this.form.email) {
      this.error = 'Username and email are required.';
      return;
    }
    this.saving = true;
    this.error = '';

    const done = () => { this.saving = false; this.editing = false; this.load(); };
    const fail = (res: ApiResponse<any>) => {
      this.saving = false;
      this.error = (res.errors && res.errors.join('; ')) || res.message || 'Save failed.';
    };

    if (this.form.id) {
      const req: UpdateUserRequest = {
        username: this.form.username,
        email: this.form.email,
        password: this.form.password || undefined,
        roleId: this.form.roleId,
        outletId: this.form.outletId,
        isActive: this.form.isActive
      };
      this.api.updateUser(this.form.id, req).subscribe({
        next: (r) => r.success ? done() : fail(r),
        error: (e) => { this.saving = false; this.error = e?.error?.errors?.join('; ') || 'Save failed.'; }
      });
    } else {
      if (!this.form.password || this.form.password.length < 12) {
        this.error = 'Password must be at least 12 characters.';
        this.saving = false;
        return;
      }
      const req: CreateUserRequest = {
        username: this.form.username,
        email: this.form.email,
        password: this.form.password,
        roleId: this.form.roleId,
        outletId: this.form.outletId
      };
      this.api.createUser(req).subscribe({
        next: (r) => r.success ? done() : fail(r),
        error: (e) => { this.saving = false; this.error = e?.error?.errors?.join('; ') || 'Create failed.'; }
      });
    }
  }

  remove(u: UserDto) {
    if (!confirm(`Deactivate and remove ${u.username}?`)) return;
    this.api.deleteUser(u.id).subscribe({
      next: (r) => { if (r.success) this.load(); },
      error: (e) => alert(e?.error?.errors?.join('; ') || 'Delete failed.')
    });
  }
}
